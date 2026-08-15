using System.Runtime.InteropServices;
using System.Text.Json;
using SwBridge;
using swmcp.server.Models;

namespace swmcp.server.Services
{
    /// <summary>Cheap document-state snapshot attached to every result, for diagnosing a failure without a second round trip.</summary>
    public sealed record DocumentStateSnapshot(string DocumentName, bool InSketchMode, int FeatureCount, int SelectionCount);

    /// <summary>Result of running one operation: never throws for a SolidWorks-side failure — see <see cref="Success"/>/<see cref="Error"/>.</summary>
    public sealed record OperationResult(bool Success, string? Error, object? Return, DocumentStateSnapshot? DocumentState);

    /// <summary>
    /// Executes one <see cref="OperationRecipe"/> against one document (or the
    /// application, for <c>scope: "application"</c> recipes): resolves the
    /// target via <see cref="ComPath"/>, binds named args to a positional
    /// array (unit parsing, type coercion, <see cref="DispatchWrapper"/> for
    /// <c>comNull</c> params), checks declared preconditions (refuses, never
    /// satisfies — ADR 0001 §1), invokes via <see cref="ComInvoker"/>, and
    /// evaluates the declared post-conditions (ADR 0002) — all inside one
    /// <see cref="SwDispatcher.Run{T}(Func{T})"/> call (ADR 0003).
    /// </summary>
    public class OperationRunner
    {
        private readonly SwConnection _connection;
        private readonly DocumentManager _documents;

        public OperationRunner(SwConnection connection, DocumentManager documents)
        {
            _connection = connection;
            _documents = documents;
        }

        public OperationResult Run(OperationRecipe recipe, string? documentName, IReadOnlyDictionary<string, JsonElement>? args) =>
            _connection.Dispatcher.Run(() => RunUnsynchronized(recipe, documentName, args));

        private OperationResult RunUnsynchronized(
            OperationRecipe recipe, string? documentName, IReadOnlyDictionary<string, JsonElement>? args)
        {
            var isDocumentScoped = string.Equals(recipe.Scope, "document", StringComparison.OrdinalIgnoreCase);

            SwDocument? doc = null;
            if (isDocumentScoped)
            {
                if (string.IsNullOrWhiteSpace(documentName))
                {
                    return Fail($"documentName is required for document-scoped operation '{recipe.Name}'. Open documents: {DescribeOpenDocuments()}");
                }

                doc = _documents.Resolve(documentName);
                if (doc == null)
                {
                    return Fail($"No open document matches '{documentName}'. Open documents: {DescribeOpenDocuments()}");
                }
            }

            var (positional, bindError) = Bind(recipe, args);
            if (bindError != null)
            {
                return Fail(bindError, doc);
            }

            if (doc != null)
            {
                var (ok, requireError) = CheckRequires(recipe, doc);
                if (!ok)
                {
                    return Fail(requireError!, doc);
                }
            }

            // new_part is a SwBridge capability (DocumentManager.NewPart), not a
            // raw COM member on ISldWorks — it resolves the default template and
            // wraps the created ModelDoc2, both SwBridge policy per ADR 0001 §5
            // ("creating a document is a SolidWorks capability, not a policy").
            // Deliberate deviation from strict ComPath/ComInvoker dispatch for
            // this one recipe: scope "application" + member "NewPart" is a
            // reserved combination the runner special-cases.
            if (string.Equals(recipe.Scope, "application", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(recipe.Member, "NewPart", StringComparison.OrdinalIgnoreCase))
            {
                return RunNewPart(positional);
            }

            var root = doc != null ? (object)doc.Model : _connection.GetApp();

            int? preFeatureCount = doc != null && recipe.Verify.Any(v => Is(v.Check, "featureCountIncreased"))
                ? DocumentStateProbes.GetFeatureCount(doc.Model)
                : null;
            int? preSketchSegCount = doc != null && recipe.Verify.Any(v => Is(v.Check, "sketchSegmentCountIncreased"))
                ? DocumentStateProbes.GetSketchSegmentCount(doc.Model)
                : null;

            var pathResult = ComPath.Resolve(root, recipe.Target ?? "");
            if (!pathResult.Success)
            {
                return Fail(
                    $"Could not resolve target '{recipe.Target}' for '{recipe.Name}' (failed at " +
                    $"'{pathResult.FailedSegment}': {pathResult.FailureDetail}). Use describe_com_members to discover valid dotted paths.",
                    doc);
            }

            InvokeOutcome outcome = recipe.Kind.ToLowerInvariant() switch
            {
                "method" => ComInvoker.InvokeMethod(pathResult.Value, recipe.Member, positional),
                "propertyget" => ComInvoker.GetProperty(pathResult.Value, recipe.Member),
                "propertyset" => ComInvoker.SetProperty(pathResult.Value, recipe.Member, positional.Length > 0 ? positional[0] : null),
                _ => InvokeOutcome.Fail($"Unknown 'kind' value '{recipe.Kind}'."),
            };

            if (!outcome.Success)
            {
                return Fail($"Invoking '{recipe.Member}' failed: {outcome.FailureDetail}", doc);
            }

            var verifyFailures = new List<string>();
            foreach (var v in recipe.Verify)
            {
                EvaluateVerify(v, doc, outcome, preFeatureCount, preSketchSegCount, verifyFailures);
            }

            var converted = ConvertReturn(recipe.Returns, outcome.Value);

            if (verifyFailures.Count > 0)
            {
                return Fail(
                    $"'{recipe.Name}' invoked without a COM error, but its declared post-conditions did not hold: " +
                    string.Join(" ", verifyFailures) +
                    " SolidWorks write APIs frequently report failure by returning Nothing/False rather than " +
                    "throwing (ADR 0002); the document was left exactly as it is — no automatic rollback was attempted.",
                    doc);
            }

            return Ok(converted, doc);
        }

        private OperationResult RunNewPart(object?[] positional)
        {
            string? templatePath = positional.Length > 0 && positional[0] is string s && !string.IsNullOrWhiteSpace(s) ? s : null;
            try
            {
                var newDoc = _documents.NewPart(templatePath);
                var info = newDoc.Info;
                var dto = new { title = info.Title, path = info.Path, type = info.Type.ToString() };
                return Ok(dto, newDoc);
            }
            catch (SwBridgeException ex)
            {
                return Fail($"new_part failed: {ex.Message}");
            }
        }

        // ------------------------------------------------------------ requires

        private static (bool Ok, string? Error) CheckRequires(OperationRecipe recipe, SwDocument doc)
        {
            foreach (var req in recipe.Requires)
            {
                switch (req.Check.ToLowerInvariant())
                {
                    case "documenttype":
                    {
                        var actual = doc.Info.Type.ToString();
                        if (!string.Equals(actual, req.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            return (false, $"Precondition 'documentType' failed: '{doc.Info.Title}' is a {actual}, this operation needs a {req.Value}.");
                        }

                        break;
                    }

                    case "insketchmode":
                        if (!DocumentStateProbes.IsInSketchMode(doc.Model))
                        {
                            return (false, "Precondition 'inSketchMode' failed: no active sketch. Call 'insert_sketch' first.");
                        }

                        break;

                    case "notinsketchmode":
                        if (DocumentStateProbes.IsInSketchMode(doc.Model))
                        {
                            return (false, "Precondition 'notInSketchMode' failed: a sketch is currently being edited. Call 'exit_sketch' first.");
                        }

                        break;

                    case "selectioncount":
                    {
                        var count = DocumentStateProbes.GetSelectionCount(doc.Model);
                        if (req.Min.HasValue && count < req.Min.Value)
                        {
                            return (false, $"Precondition 'selectionCount' failed: {count} entities selected, need at least {req.Min}. Call 'select_by_id' first.");
                        }

                        if (req.Max.HasValue && count > req.Max.Value)
                        {
                            return (false, $"Precondition 'selectionCount' failed: {count} entities selected, at most {req.Max} allowed. Call 'clear_selection' first.");
                        }

                        break;
                    }

                    case "selectiontype":
                    {
                        var (ok, detail) = CheckSelectionType(doc, req);
                        if (!ok)
                        {
                            return (false, detail);
                        }

                        break;
                    }

                    default:
                        return (false, $"Unknown precondition check '{req.Check}' in recipe '{recipe.Name}'.");
                }
            }

            return (true, null);
        }

        // Not a closed-form SwBridge probe (DocumentStateProbes has none for
        // selection type) — evaluated generically via ComPath/ComInvoker
        // against SelectionMgr, per ADR 0001's "open path grammar" rationale.
        // 'req.Type', when present, is a swSelectType_e integer given as a
        // string — a documented simplification of the ADR's unspecified
        // selectionType(type, mark) shape (see OperationRecipe.cs remarks).
        private static (bool Ok, string? Detail) CheckSelectionType(SwDocument doc, RequireCheck req)
        {
            if (!req.Mark.HasValue)
            {
                return (false, "Precondition 'selectionType' is missing 'mark' in the recipe.");
            }

            var pathResult = ComPath.Resolve(doc.Model, "SelectionManager");
            if (!pathResult.Success)
            {
                return (false, $"Precondition 'selectionType' could not resolve SelectionManager: {pathResult.FailureDetail}");
            }

            var countOutcome = ComInvoker.InvokeMethod(pathResult.Value, "GetSelectedObjectCount2", new object?[] { req.Mark.Value });
            var count = countOutcome.Success && countOutcome.Value is int c ? c : 0;
            if (count < 1)
            {
                return (false, $"Precondition 'selectionType' failed: nothing selected at mark {req.Mark}. Call 'select_by_id' with mark {req.Mark} first.");
            }

            if (!string.IsNullOrWhiteSpace(req.Type) && int.TryParse(req.Type, out var expectedType))
            {
                var typeOutcome = ComInvoker.InvokeMethod(pathResult.Value, "GetSelectedObjectType3", new object?[] { 1, req.Mark.Value });
                if (typeOutcome.Success && typeOutcome.Value is int actualType && actualType != expectedType)
                {
                    return (false, $"Precondition 'selectionType' failed: selection at mark {req.Mark} has swSelectType_e {actualType}, expected {expectedType}.");
                }
            }

            return (true, null);
        }

        // ------------------------------------------------------------- verify

        private static void EvaluateVerify(
            VerifyCheck v, SwDocument? doc, InvokeOutcome outcome, int? preFeatureCount, int? preSketchSegCount, List<string> failures)
        {
            switch (v.Check.ToLowerInvariant())
            {
                case "returnnotnull":
                    if (outcome.Value == null)
                    {
                        failures.Add("returnNotNull: the call returned null/nothing.");
                    }

                    break;

                case "returntrue":
                    if (outcome.Value is not bool b || !b)
                    {
                        failures.Add($"returnTrue: the call returned {Describe(outcome.Value)}, expected true.");
                    }

                    break;

                case "featurecountincreased":
                {
                    if (doc == null || preFeatureCount == null)
                    {
                        failures.Add("featureCountIncreased: no document to probe.");
                        break;
                    }

                    var post = DocumentStateProbes.GetFeatureCount(doc.Model);
                    var expectedBy = v.By ?? 1;
                    var actualBy = post - preFeatureCount.Value;
                    if (actualBy < expectedBy)
                    {
                        failures.Add($"featureCountIncreased: expected +{expectedBy}, observed {actualBy} ({preFeatureCount}->{post}).");
                    }

                    break;
                }

                case "sketchsegmentcountincreased":
                {
                    if (doc == null || preSketchSegCount == null)
                    {
                        failures.Add("sketchSegmentCountIncreased: no document to probe.");
                        break;
                    }

                    var post = DocumentStateProbes.GetSketchSegmentCount(doc.Model);
                    var expectedBy = v.By ?? 1;
                    var actualBy = post - preSketchSegCount.Value;
                    if (actualBy < expectedBy)
                    {
                        failures.Add($"sketchSegmentCountIncreased: expected +{expectedBy}, observed {actualBy} ({preSketchSegCount}->{post}).");
                    }

                    break;
                }

                case "sketchmodeis":
                {
                    if (doc == null)
                    {
                        failures.Add("sketchModeIs: no document to probe.");
                        break;
                    }

                    var mode = DocumentStateProbes.IsInSketchMode(doc.Model);
                    var expected = v.Value ?? true;
                    if (mode != expected)
                    {
                        failures.Add($"sketchModeIs: expected {expected}, observed {mode}.");
                    }

                    break;
                }

                case "nonewrebuilderrors":
                    if (doc == null)
                    {
                        failures.Add("noNewRebuildErrors: no document to probe.");
                        break;
                    }

                    if (!DocumentStateProbes.RebuildSucceeded(doc.Model))
                    {
                        failures.Add("noNewRebuildErrors: EditRebuild3 reported errors.");
                    }

                    break;

                default:
                    failures.Add($"Unknown verify check '{v.Check}' — treated as failed.");
                    break;
            }
        }

        private static string Describe(object? v) => v switch { null => "null", bool bb => bb.ToString(), _ => v.ToString() ?? "?" };

        private static bool Is(string check, string name) => string.Equals(check, name, StringComparison.OrdinalIgnoreCase);

        // -------------------------------------------------------------- bind

        // Internal (not private) so swmcp.server.tests can exercise the pure
        // argument-binding/unit-parsing logic directly, without SolidWorks.
        internal static (object?[] Positional, string? Error) Bind(OperationRecipe recipe, IReadOnlyDictionary<string, JsonElement>? args)
        {
            var positional = new object?[recipe.Params.Count];
            for (var i = 0; i < recipe.Params.Count; i++)
            {
                var p = recipe.Params[i];

                if (string.Equals(p.Type, "comNull", StringComparison.OrdinalIgnoreCase))
                {
                    // Verified live: a bare null for a COM-interface parameter
                    // (e.g. SelectByID2's Callout) marshals as VT_EMPTY and
                    // SolidWorks rejects it with DISP_E_TYPEMISMATCH. Callers
                    // never supply a value for this param type.
                    positional[i] = new DispatchWrapper(null);
                    continue;
                }

                JsonElement? raw = null;
                if (args != null && args.TryGetValue(p.Name, out var supplied))
                {
                    raw = supplied;
                }
                else if (p.HasDefault)
                {
                    raw = p.Default;
                }
                else if (p.Required)
                {
                    var hint = string.IsNullOrEmpty(p.Description) ? "" : $" — {p.Description}";
                    return (Array.Empty<object?>(), $"Missing required parameter '{p.Name}' ({p.Type}){hint}.");
                }

                var (value, error) = ConvertParam(p, raw);
                if (error != null)
                {
                    return (Array.Empty<object?>(), $"Parameter '{p.Name}': {error}");
                }

                positional[i] = value;
            }

            return (positional, null);
        }

        internal static (object? Value, string? Error) ConvertParam(OperationParam p, JsonElement? raw)
        {
            try
            {
                switch (p.Type.ToLowerInvariant())
                {
                    case "bool":
                        return (raw.HasValue ? (object)ToBool(raw.Value) : false, null);
                    case "int":
                    case "enum":
                        return (raw.HasValue ? (object)ToInt(raw.Value) : 0, null);
                    case "double":
                        return (raw.HasValue ? (object)ToDouble(raw.Value) : 0.0, null);
                    case "string":
                        return (raw.HasValue ? ToStringValue(raw.Value) : "", null);
                    case "length":
                        if (!raw.HasValue)
                        {
                            return (0.0, null);
                        }

                        return UnitParser.TryParseLength(raw.Value, out var meters, out var lengthError)
                            ? (meters, (string?)null)
                            : ((object?)null, lengthError);
                    case "angle":
                        if (!raw.HasValue)
                        {
                            return (0.0, null);
                        }

                        return UnitParser.TryParseAngle(raw.Value, out var radians, out var angleError)
                            ? (radians, (string?)null)
                            : ((object?)null, angleError);
                    default:
                        return (null, $"Unknown parameter type '{p.Type}'.");
                }
            }
            catch (Exception ex)
            {
                return (null, $"Could not convert value for type '{p.Type}': {ex.Message}");
            }
        }

        private static bool ToBool(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.Parse(e.GetString() ?? "false"),
            JsonValueKind.Number => e.GetDouble() != 0,
            _ => throw new FormatException($"Cannot interpret JSON {e.ValueKind} as bool."),
        };

        private static int ToInt(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Number => e.TryGetInt32(out var i) ? i : (int)e.GetDouble(),
            JsonValueKind.String => int.Parse(e.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new FormatException($"Cannot interpret JSON {e.ValueKind} as int."),
        };

        private static double ToDouble(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.String => double.Parse(e.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new FormatException($"Cannot interpret JSON {e.ValueKind} as double."),
        };

        private static string ToStringValue(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ToString();

        // ------------------------------------------------------------ return

        private static object? ConvertReturn(ReturnsSpec? returns, object? raw)
        {
            var type = returns?.Type?.ToLowerInvariant() ?? "void";
            return type switch
            {
                "void" => null,
                "bool" => raw is bool bb ? bb : raw != null && Convert.ToBoolean(raw),
                "number" => raw,
                "string" => raw as string,
                "feature" => ResultConverters.ToFeatureRef(raw),
                "sketchsegment" => ResultConverters.ToSketchSegmentRef(raw),
                "sketchsegments" => ResultConverters.ToSketchSegmentRefs(ToObjectEnumerable(raw)),
                _ => raw,
            };
        }

        private static IEnumerable<object?>? ToObjectEnumerable(object? raw) => raw switch
        {
            null => null,
            object[] arr => arr,
            System.Collections.IEnumerable en => en.Cast<object?>(),
            _ => null,
        };

        // ----------------------------------------------------------- helpers

        private static OperationResult Ok(object? ret, SwDocument? doc) => new(true, null, ret, Snapshot(doc));

        private static OperationResult Fail(string error, SwDocument? doc = null) => new(false, error, null, Snapshot(doc));

        private static DocumentStateSnapshot? Snapshot(SwDocument? doc)
        {
            if (doc == null)
            {
                return null;
            }

            return new DocumentStateSnapshot(
                doc.Info.Title,
                DocumentStateProbes.IsInSketchMode(doc.Model),
                DocumentStateProbes.GetFeatureCount(doc.Model),
                DocumentStateProbes.GetSelectionCount(doc.Model));
        }

        private string DescribeOpenDocuments() =>
            string.Join(", ", _documents.ListOpenDocuments().Select(d => $"{d.Title} ({d.Type})"));
    }
}
