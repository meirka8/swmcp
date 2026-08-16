using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
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

        public SolidWorksTool(DocumentManager documents, SchemaManager schemaManager)
        {
            _documents = documents;
            _schemaManager = schemaManager;
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
            "Gets information about an open SolidWorks part: path, title, mass, bounding box, and the " +
            "feature tree with per-feature properties for known feature types. Specify documentName " +
            "(title, file name, or path) when more than one document is open. A documentName matching " +
            "more than one open document is refused rather than guessed.")]
        public object GetPartInfo(
            [Description("Which open document to inspect; may be omitted when exactly one document is open.")]
            string? documentName = null)
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

                return new
                {
                    partInfo.Path,
                    partInfo.Title,
                    partInfo.Mass,
                    Features = partInfo.Features.Select(f => new
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
