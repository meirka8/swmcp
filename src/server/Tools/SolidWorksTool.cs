using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SolidWorks.Interop.sldworks;
using SwBridge;
using swmcp.server.Services;

namespace swmcp.server.Tools
{
    /// <summary>One property entry of a feature schema, as supplied by the client.</summary>
    public class PropertySpecInput
    {
        [Description("Key under which the value is reported, e.g. 'Depth'.")]
        public string Name { get; set; } = "";

        [Description("COM member on the feature definition to read (case-insensitive), e.g. 'GetDepth' or 'BothDirections'. Defaults to Name.")]
        public string? Member { get; set; }

        [Description("Arguments for accessor methods, e.g. [true] for GetDepth(true). Omit for bare properties.")]
        public JsonElement[]? Args { get; set; }
    }

    [McpServerToolType]
    public class SolidWorksTool
    {
        private readonly DocumentManager _documents;
        private readonly SchemaManager _schemaManager;
        private readonly SwConnection _connection;

        public SolidWorksTool(DocumentManager documents, SchemaManager schemaManager, SwConnection connection)
        {
            _documents = documents;
            _schemaManager = schemaManager;
            _connection = connection;
        }

        [McpServerTool, Description("Lists all documents currently open in SolidWorks (title, file path, type).")]
        public object ListOpenDocuments()
        {
            try
            {
                return new { documents = _documents.ListOpenDocuments() };
            }
            catch (SwBridgeException ex)
            {
                return new { error = ex.Message };
            }
        }

        [McpServerTool, Description(
            "Gets information about an open SolidWorks part: path, title, mass, material, density, bounding box, and " +
            "the feature tree with per-feature properties for known feature types. Specify documentName " +
            "(title, file name, or path) when more than one document is open. A documentName matching " +
            "more than one open document is refused rather than guessed. By default the feature tree omits permanent " +
            "tree-plumbing entries (folders, lights — see includeFolderFeatures) that carry no geometry information " +
            "and would otherwise be 19 of a typical 25-entry list.")]
        public object GetPartInfo(
            [Description("Which open document to inspect; may be omitted when exactly one document is open.")]
            string? documentName = null,
            [Description(
                "When false (default), feature-tree entries that are permanent tree plumbing — folders " +
                "(Comments, Favorites, History, ...), the material folder, notes, lights — are omitted; they carry no " +
                "geometry information and are the same 16-19 entries on every part regardless of what was modeled. " +
                "Set true to see the full, unfiltered tree exactly as SolidWorks' FeatureManager reports it.")]
            bool includeFolderFeatures = false)
        {
            try
            {
                var doc = ResolveDocument(documentName, out var error);
                if (doc == null)
                {
                    return new { error };
                }

                var partInfo = doc.GetPartInfo(_schemaManager.GetSchema);
                if (partInfo == null)
                {
                    return new { error = $"Document '{doc.Info.Title}' is not a part with solid bodies." };
                }

                var (material, density) = ReadMaterialInfo(doc);
                var features = FeatureTreeFilter.Apply(partInfo.Features, includeFolderFeatures);

                return new
                {
                    partInfo.Path,
                    partInfo.Title,
                    partInfo.Mass,
                    Material = material,
                    Density = density,
                    Features = features.Select(f => new
                    {
                        f.Name,
                        f.TypeName,
                        Known = f.Properties != null,
                        Data = f.Properties,
                    }),
                    partInfo.BoundingBox,
                };
            }
            catch (SwBridgeException ex)
            {
                // Covers SwNotRunningException (SolidWorks closed) and, since
                // SwBridge 0.5.0, the SwBridgeException DocumentManager.Resolve
                // throws when documentName is ambiguous — that used to escape
                // as an unhandled exception here.
                return new { error = ex.Message };
            }
        }

        [McpServerTool, Description(
            "Read-only snapshot of a document's live state: whether a sketch is being edited (and its name, if " +
            "available), feature and selection counts, the identity of what is currently selected, and whether a " +
            "rebuild is outstanding. Passive probes only — nothing here writes to the document or forces a rebuild " +
            "(unlike the 'rebuild' operation or the noNewRebuildErrors verify check). Use this after reconnecting to a " +
            "session you did not start (e.g. after a client crash) to discover a dangling sketch or stale selection " +
            "without having to attempt a write first, or mid-plan to confirm ambient state before the next step.")]
        public object GetDocumentState(
            [Description("Which open document to inspect (title, file name, or path). Required — no active-document fallback.")]
            string documentName)
        {
            try
            {
                var doc = _documents.Resolve(documentName);
                if (doc == null)
                {
                    return new { error = $"No open document matches '{documentName}'. Open documents: {DescribeOpenDocuments()}" };
                }

                return _connection.Dispatcher.Run<object>(() =>
                {
                    var inSketchMode = DocumentStateProbes.IsInSketchMode(doc.Model);
                    var activeSketchName = inSketchMode ? ReadActiveSketchName(doc.Model) : null;
                    var featureCount = DocumentStateProbes.GetFeatureCount(doc.Model);
                    var selectionCount = DocumentStateProbes.GetSelectionCount(doc.Model);
                    var selectedEntities = selectionCount > 0 ? SelectionInspector.GetSelection(doc.Model) : null;
                    var needsRebuild = DocumentStateProbes.NeedsRebuild(doc.Model);

                    return new
                    {
                        documentName = doc.Info.Title,
                        inSketchMode,
                        activeSketch = activeSketchName,
                        featureCount,
                        selectionCount,
                        selectedEntities,
                        needsRebuild,
                    };
                });
            }
            catch (SwBridgeException ex)
            {
                return new { error = ex.Message };
            }
        }

        [McpServerTool, Description(
            "Registers (or replaces) the property schema for a SolidWorks feature type, teaching the server " +
            "how to read that feature's definition. Feature type names come from IFeature.GetTypeName2() " +
            "(e.g. 'Extrusion', 'Fillet', 'CirPattern'). Each property is read off the COM definition object " +
            "either as a bare property (member with no args, e.g. 'BothDirections') or an accessor method " +
            "with arguments (e.g. member 'GetDepth', args [true]). The schema persists across sessions.")]
        public object RegisterFeatureSchema(
            [Description("Feature type name as returned by GetTypeName2(), e.g. 'Extrusion'.")]
            string featureType,
            [Description("The properties to read for this feature type.")]
            PropertySpecInput[] properties)
        {
            var specs = new List<PropertySpec>();
            foreach (var input in properties)
            {
                if (string.IsNullOrWhiteSpace(input.Name))
                {
                    return new { error = "Every property needs a non-empty 'name'." };
                }

                List<object?>? args = null;
                if (input.Args is { Length: > 0 })
                {
                    args = input.Args.Select(SchemaManager.ToClrValue).ToList();
                }

                specs.Add(new PropertySpec(input.Name, input.Member ?? input.Name, args));
            }

            _schemaManager.RegisterSchema(featureType, specs);
            return new { registered = featureType, propertyCount = specs.Count };
        }

        // Gap #4 (UAT re-verdict): get_part_info reported mass with no way to
        // confirm what material (if any) produced it — an unassigned part
        // computes at water's density (1000 kg/m^3), a plausible-looking
        // number that means nothing. material/density read the same way every
        // other late-bound value in this codebase does (ComPropertyReader),
        // never via a strongly-typed interop cast — consistent with "swmcp
        // contains no interop code" even where SwBridge's raw ModelDoc2
        // escape hatch would make a typed cast easy to write.
        private (string? Material, double? Density) ReadMaterialInfo(SwDocument doc) =>
            _connection.Dispatcher.Run(() =>
            {
                // IPartDoc.GetMaterialPropertyName2's second parameter is a
                // ByRef 'out' string (the material database). Verified live
                // (tests/SeedVerifier's 'material' mode): ComPropertyReader's
                // late-bound Type.InvokeMember cannot call this member at all —
                // it fails identically whether the ByRef slot is supplied,
                // omitted, or null — because InvokeMember needs a
                // ParameterModifier array to marshal a COM ByRef argument
                // correctly and SwBridge's reader does not use that overload
                // (by design: it exists for read-only bare-property/no-output-
                // param access). An early-bound cast is the one live-verified
                // way to read this value; every other read in this codebase
                // stays late-bound via ComPropertyReader, including Density
                // just below, which has no ByRef parameter and works late-bound
                // exactly as expected.
                string? material = null;
                if (doc.Model is PartDoc partDoc)
                {
                    var materialName = partDoc.GetMaterialPropertyName2("", out _);
                    if (!string.IsNullOrWhiteSpace(materialName))
                    {
                        material = materialName;
                    }
                }

                double? density = null;
                object? extension = null;
                object? massProperty = null;
                try
                {
                    if (ComPropertyReader.TryGetProperty(doc.Model, "Extension", out extension) && extension != null &&
                        ComPropertyReader.TryGetMember(extension, "CreateMassProperty", null, out massProperty) && massProperty != null &&
                        ComPropertyReader.TryGetProperty(massProperty, "Density", out var densityValue) && densityValue is double d)
                    {
                        density = d;
                    }
                }
                finally
                {
                    ComLifetime.Release(massProperty);
                    ComLifetime.Release(extension);
                }

                return (material, density);
            });

        // Best-effort: ISketch declares no Name member in the interop
        // assembly (verified statically — SeedVerifier `members ISketch`),
        // but the live object behind ActiveSketch is also the owning
        // Feature, which does, and late-bound IDispatch lookup by name
        // resolves against whatever the object actually implements, not the
        // narrower ISketch reference type. Null (not a thrown exception) if
        // that does not hold on some sketch — this is explicitly "if cheeply
        // available", never a reason to fail the whole read.
        private static string? ReadActiveSketchName(object modelDoc)
        {
            object? sketchManager = null;
            object? activeSketch = null;
            try
            {
                if (!ComPropertyReader.TryGetProperty(modelDoc, "SketchManager", out sketchManager) || sketchManager == null)
                {
                    return null;
                }

                if (!ComPropertyReader.TryGetProperty(sketchManager, "ActiveSketch", out activeSketch) || activeSketch == null)
                {
                    return null;
                }

                return ComPropertyReader.TryGetProperty(activeSketch, "Name", out var nameValue) &&
                       nameValue is string name && !string.IsNullOrEmpty(name)
                    ? name
                    : null;
            }
            finally
            {
                ComLifetime.Release(activeSketch);
                ComLifetime.Release(sketchManager);
            }
        }

        private SwDocument? ResolveDocument(string? documentName, out string? error)
        {
            error = null;
            if (documentName != null)
            {
                var resolved = _documents.Resolve(documentName);
                if (resolved == null)
                {
                    error = $"No open document matches '{documentName}'. Open documents: {DescribeOpenDocuments()}";
                }
                return resolved;
            }

            var open = _documents.GetOpenDocuments();
            if (open.Count == 1)
            {
                return open[0];
            }

            error = open.Count == 0
                ? "No documents are open in SolidWorks."
                : $"Multiple documents are open — specify documentName. Open documents: {DescribeOpenDocuments()}";
            return null;
        }

        private string DescribeOpenDocuments()
        {
            try
            {
                return string.Join(", ", _documents.ListOpenDocuments().Select(d => $"{d.Title} ({d.Type})"));
            }
            catch (SwBridgeException ex)
            {
                return $"(could not list open documents: {ex.Message})";
            }
        }
    }
}
