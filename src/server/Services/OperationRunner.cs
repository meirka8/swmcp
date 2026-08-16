using System.Runtime.InteropServices;
using System.Text.Json;
using SwBridge;
using swmcp.server.Models;

namespace swmcp.server.Services
{
    /// <summary>Cheap document-state snapshot attached to every result, for diagnosing a failure without a second round trip.</summary>
    public sealed record DocumentStateSnapshot(string DocumentName, bool InSketchMode, int FeatureCount, int SelectionCount);

    /// <summary>
    /// Result of running one operation: never throws for a SolidWorks-side
    /// failure — see <see cref="Success"/>/<see cref="Error"/>.
    /// </summary>
    /// <param name="BoundArgs">
    /// The final, named, SI-unit values actually bound to the COM call (added
    /// post-UAT B1) — e.g. <c>{"depth1": 0.04}</c>, never <c>{"depth1": "40"}</c>.
    /// Null only when the operation failed before binding completed (missing
    /// <c>documentName</c>, document not found). This is what makes a silent
    /// 1000x unit error or a mistyped parameter visible in the transcript
    /// instead of merely absent from it.
    /// </param>
    public sealed record OperationResult(
        bool Success, string? Error, object? Return, DocumentStateSnapshot? DocumentState,
        IReadOnlyDictionary<string, object?>? BoundArgs);

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

        /// <summary>Runs one operation, using <see cref="SwDispatcher.DefaultTimeout"/>.</summary>
        public OperationResult Run(OperationRecipe recipe, string? documentName, IReadOnlyDictionary<string, JsonElement>? args) =>
            _connection.Dispatcher.Run(() => RunUnsynchronized(recipe, documentName, args));

        /// <summary>
        /// Runs an ordered batch of operations as <b>one</b> unit of work on the
        /// dispatcher (post-review M1) — the whole plan, not one dispatch per
        /// step, so no other queued request can interleave mid-batch and mutate
        /// the ambient state (active sketch, selection list) a later step
        /// depends on. Fails fast: stops at the first step whose
        /// <see cref="OperationResult.Success"/> is false. <paramref name="timeout"/>
        /// should be generous — the whole batch shares it, not each step
        /// individually (the caller is expected to scale it with step count,
        /// e.g. 120s + 30s/step).
        /// </summary>
        public IReadOnlyList<OperationResult> RunBatch(
            IReadOnlyList<(OperationRecipe Recipe, IReadOnlyDictionary<string, JsonElement>? Args)> steps,
            string? documentName,
            TimeSpan timeout) =>
            _connection.Dispatcher.Run(
                () =>
                {
                    var results = new List<OperationResult>();
                    foreach (var (recipe, args) in steps)
                    {
                        var result = RunUnsynchronized(recipe, documentName, args);
                        results.Add(result);
                        if (!result.Success)
                        {
                            break;
                        }
                    }

                    return (IReadOnlyList<OperationResult>)results;
                },
                timeout);

        // Never lets an exception escape (defense for RunBatch: a raw throw here
        // would abort the whole batch's dispatch and lose every already-completed
        // step's result — see code review H5). SwBridgeException covers
        // SwNotRunningException, SwDispatchTimeoutException (from a misbehaving
        // nested call) and DocumentManager.Resolve's new ambiguous-match throw;
        // COMException/InvalidComObjectException cover a COM call that faults
        // outside ComInvoker's own try/catch (e.g. while reading doc.Info).
        private OperationResult RunUnsynchronized(
            OperationRecipe recipe, string? documentName, IReadOnlyDictionary<string, JsonElement>? args)
        {
            try
            {
                return RunUnsynchronizedCore(recipe, documentName, args);
            }
            catch (Exception ex) when (ex is SwBridgeException or COMException or InvalidComObjectException)
            {
                return Fail($"'{recipe.Name}' could not run: {ex.Message}");
            }
        }

        private OperationResult RunUnsynchronizedCore(
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

                // DocumentManager.Resolve (SwBridge 0.5.0) throws SwBridgeException
                // when documentName matches more than one open document, rather
                // than silently picking the first enumerated — caught by
                // RunUnsynchronized's wrapper above, reported the same way as any
                // other refusal.
                doc = _documents.Resolve(documentName);
                if (doc == null)
                {
                    return Fail($"No open document matches '{documentName}'. Open documents: {DescribeOpenDocuments()}");
                }
            }

            var (positional, boundArgs, bindError) = Bind(recipe, args);
            if (bindError != null)
            {
                return Fail(bindError, doc);
            }

            if (doc != null)
            {
                var (ok, requireError) = CheckRequires(recipe, doc);
                if (!ok)
                {
                    return Fail(requireError!, doc, boundArgs);
                }
            }

            // M4: application-scoped recipes cannot declare 'requires' — rejected
            // at OperationManager.Validate (registration time), not re-checked
            // here at run time. Chosen over a runtime fail-closed check (the code
            // review offered both; the coordinator asked for one) because the
            // earlier failure point costs nothing at call time and gives the
            // author feedback the moment they try to register the bad recipe,
            // rather than the first time it happens to run. The shipped seed's
            // only application-scoped recipe (new_part) has an empty requires
            // list, so this is never reachable through normal channels; a
            // hand-edited known_operations.json bypassing register_operation's
            // validation is the one way it could be, and Validate would refuse
            // it on the next register_operation call regardless.

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
                return RunNewPart(recipe, positional, boundArgs);
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
                    doc, boundArgs);
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
                return Fail($"Invoking '{recipe.Member}' failed: {outcome.FailureDetail}", doc, boundArgs);
            }

            var verifyFailures = new List<string>();
            foreach (var v in recipe.Verify)
            {
                EvaluateVerify(v, doc, outcome, preFeatureCount, preSketchSegCount, verifyFailures);
            }

            // C2: never let a raw RCW leave the dispatch. ConvertReturn refuses
            // (and releases) anything it cannot safely convert, rather than
            // falling through to "return the raw value" — ownsReference is false
            // whenever the returned object is reference-equal to the document
            // model or the resolved invocation target, since those are shared
            // handles other code still holds (H4: releasing a shared RCW
            // disconnects it for every holder, permanently).
            var ownsReference = !ReferenceEquals(outcome.Value, doc?.Model) && !ReferenceEquals(outcome.Value, pathResult.Value);
            var (converted, convertError) = ConvertReturn(recipe.Returns, outcome.Value, ownsReference);
            if (convertError != null)
            {
                return Fail(convertError, doc, boundArgs);
            }

            if (verifyFailures.Count > 0)
            {
                return Fail(
                    $"'{recipe.Name}' invoked without a COM error, but its declared post-conditions did not hold: " +
                    string.Join(" ", verifyFailures) +
                    " SolidWorks write APIs frequently report failure by returning Nothing/False rather than " +
                    "throwing (ADR 0002); the document was left exactly as it is — no automatic rollback was attempted.",
                    doc, boundArgs);
            }

            return Ok(converted, doc, boundArgs);
        }

        private OperationResult RunNewPart(OperationRecipe recipe, object?[] positional, IReadOnlyDictionary<string, object?> boundArgs)
        {
            string? templatePath = positional.Length > 0 && positional[0] is string s && !string.IsNullOrWhiteSpace(s) ? s : null;
            SwDocument newDoc;
            try
            {
                newDoc = _documents.NewPart(templatePath);
            }
            catch (Exception ex) when (ex is SwBridgeException or COMException)
            {
                // M6: widened from SwBridgeException-only — a COMException from
                // app.NewDocument (e.g. a corrupt .prtdot, a template on an
                // unreachable network share) previously escaped as an unhandled
                // exception instead of the {success:false} shape every other
                // failure uses.
                return Fail($"new_part failed: {ex.Message}", boundArgs: boundArgs);
            }

            var info = newDoc.Info;
            var dto = new { title = info.Title, path = info.Path, type = info.Type.ToString() };

            // M6: new_part previously returned Ok(...) unconditionally, skipping
            // recipe.Verify entirely — the seed's own {"check":"returnNotNull"}
            // was dead data. Evaluate it against a synthetic InvokeOutcome the
            // same way every other recipe's verify list is evaluated, so a
            // hand-registered variant of new_part with stricter verify is honored.
            var verifyFailures = new List<string>();
            foreach (var v in recipe.Verify)
            {
                EvaluateVerify(v, newDoc, InvokeOutcome.Ok(dto), preFeatureCount: null, preSketchSegCount: null, verifyFailures);
            }

            if (verifyFailures.Count > 0)
            {
                return Fail(
                    $"'new_part' created a document but its declared post-conditions did not hold: {string.Join(" ", verifyFailures)}",
                    newDoc, boundArgs);
            }

            return Ok(dto, newDoc, boundArgs);
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

        // Internal (not private) so swmcp.server.tests can exercise verify
        // predicates — notably returnEquals — directly, without SolidWorks.
        internal static void EvaluateVerify(
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

                // B5: closed vocabulary previously had no way to express "expect
                // a status code", so a successful SaveAs3 (which returns 0 =
                // swFileSaveError_e success, not a bool) was reported as a
                // verification failure despite writing a correct file to disk.
                case "returnequals":
                {
                    if (v.Expected is not { } expected)
                    {
                        failures.Add("returnEquals: recipe is missing 'expected'.");
                        break;
                    }

                    if (!ValuesEqual(outcome.Value, expected))
                    {
                        failures.Add($"returnEquals: expected {expected}, the call returned {Describe(outcome.Value)}.");
                    }

                    break;
                }

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

        private static bool ValuesEqual(object? actual, JsonElement expected)
        {
            try
            {
                return expected.ValueKind switch
                {
                    JsonValueKind.Number => actual is IConvertible conv &&
                        Math.Abs(Convert.ToDouble(conv, System.Globalization.CultureInfo.InvariantCulture) - expected.GetDouble()) < 1e-9,
                    JsonValueKind.True => actual is true,
                    JsonValueKind.False => actual is false,
                    JsonValueKind.String => actual is string s && string.Equals(s, expected.GetString(), StringComparison.Ordinal),
                    _ => false,
                };
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Describe(object? v) => v switch { null => "null", bool bb => bb.ToString(), _ => v.ToString() ?? "?" };

        private static bool Is(string check, string name) => string.Equals(check, name, StringComparison.OrdinalIgnoreCase);

        // -------------------------------------------------------------- bind

        // Internal (not private) so swmcp.server.tests can exercise the pure
        // argument-binding/unit-parsing logic directly, without SolidWorks.
        internal static (object?[] Positional, Dictionary<string, object?> BoundArgs, string? Error) Bind(
            OperationRecipe recipe, IReadOnlyDictionary<string, JsonElement>? args)
        {
            // Normalize to a case-insensitive lookup up front: the unknown-key
            // check below matches param names case-insensitively, so the
            // per-param retrieval must too, or a key that differs only in case
            // (e.g. "Mark" for a declared "mark") would pass the unknown-key
            // check as "known" and then silently miss its own lookup, falling
            // back to the default anyway — the exact silent-default failure
            // mode H3/B2 exists to prevent, just with an extra step. Args
            // supplied over MCP are plain JSON object keys with no case
            // convention guaranteed by any client.
            var normalizedArgs = args is { Count: > 0 }
                ? new Dictionary<string, JsonElement>(args, StringComparer.OrdinalIgnoreCase)
                : null;

            // H3 / UAT B2: a key in 'args' that names no declared param used to
            // be silently ignored. Since most params have defaults, a typo
            // ("marks" instead of "mark", "reverse" instead of
            // "reverseDirection") used to fall back to a default value with no
            // warning anywhere — a valid-but-wrong feature reported as success.
            // Reject the whole call instead, naming the unknown key(s) and the
            // recipe's real param list.
            if (normalizedArgs != null)
            {
                var declared = new HashSet<string>(recipe.Params.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                var unknown = normalizedArgs.Keys.Where(k => !declared.Contains(k)).ToList();
                if (unknown.Count > 0)
                {
                    var declaredList = recipe.Params.Count > 0 ? string.Join(", ", recipe.Params.Select(p => p.Name)) : "(none)";
                    return (
                        Array.Empty<object?>(),
                        new Dictionary<string, object?>(),
                        $"Unknown argument(s) {string.Join(", ", unknown.Select(u => $"'{u}'"))} for operation '{recipe.Name}'. " +
                        $"Declared params: {declaredList}. Call describe_operation for types, units and defaults.");
                }
            }

            var positional = new object?[recipe.Params.Count];
            var boundArgs = new Dictionary<string, object?>();

            for (var i = 0; i < recipe.Params.Count; i++)
            {
                var p = recipe.Params[i];
                var suppliedByCaller = normalizedArgs != null && normalizedArgs.ContainsKey(p.Name);

                if (string.Equals(p.Type, "comNull", StringComparison.OrdinalIgnoreCase))
                {
                    // A caller supplying a value for a comNull param has
                    // misunderstood something (it can only ever be a null COM
                    // interface pointer) — tell them, rather than silently
                    // discarding whatever they sent (code review H3 sidebar).
                    if (suppliedByCaller)
                    {
                        return (
                            Array.Empty<object?>(),
                            new Dictionary<string, object?>(),
                            $"Parameter '{p.Name}' is type 'comNull' and cannot be supplied a value — it is always bound to a " +
                            "null COM interface pointer. Omit it from args.");
                    }

                    // Verified live: a bare null for a COM-interface parameter
                    // (e.g. SelectByID2's Callout) marshals as VT_EMPTY and
                    // SolidWorks rejects it with DISP_E_TYPEMISMATCH.
                    positional[i] = new DispatchWrapper(null);
                    boundArgs[p.Name] = null;
                    continue;
                }

                JsonElement? raw = null;
                if (suppliedByCaller)
                {
                    raw = normalizedArgs![p.Name];
                }
                else if (p.HasDefault)
                {
                    raw = p.Default;
                }
                else if (p.Required)
                {
                    var hint = string.IsNullOrEmpty(p.Description) ? "" : $" — {p.Description}";
                    return (Array.Empty<object?>(), new Dictionary<string, object?>(), $"Missing required parameter '{p.Name}' ({p.Type}){hint}.");
                }

                // B1's unit requirement applies to what a CALLER sends, not to
                // a recipe's own declared default: a default is authored data
                // already expressed in canonical SI (the same trust boundary as
                // the recipe's target/member), not an AI client's guess at a
                // magnitude — select_by_ray's "radius": 0.0005 default, or
                // select_by_id's "x": 0, would otherwise be rejected by the
                // same gate that exists to catch a caller's silent 1000x error.
                var (value, error) = ConvertParam(p, raw, requireUnit: suppliedByCaller);
                if (error != null)
                {
                    return (Array.Empty<object?>(), new Dictionary<string, object?>(), $"Parameter '{p.Name}': {error}");
                }

                positional[i] = value;
                boundArgs[p.Name] = value;
            }

            return (positional, boundArgs, null);
        }

        internal static (object? Value, string? Error) ConvertParam(OperationParam p, JsonElement? raw, bool requireUnit = true)
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

                        if (!requireUnit && raw.Value.ValueKind == JsonValueKind.Number)
                        {
                            return (raw.Value.GetDouble(), null); // recipe-authored default: already canonical SI (meters)
                        }

                        return UnitParser.TryParseLength(raw.Value, out var meters, out var lengthError)
                            ? (meters, (string?)null)
                            : ((object?)null, lengthError);
                    case "angle":
                        if (!raw.HasValue)
                        {
                            return (0.0, null);
                        }

                        if (!requireUnit && raw.Value.ValueKind == JsonValueKind.Number)
                        {
                            return (raw.Value.GetDouble(), null); // recipe-authored default: already canonical SI (radians)
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

        // C2: the prior "_ => raw" fall-through (and the unconditional pass-
        // through for "number") could hand a live RCW straight to the MCP
        // response serializer, which runs on a thread-pool (MTA) thread — a
        // cross-apartment touch of an object that belongs to the dispatcher's
        // STA. Every branch here either converts to a plain DTO or refuses
        // (releasing the RCW first when this call owns it) — nothing but a
        // CLR primitive or a converter DTO ever leaves this method.
        private static (object? Value, string? Error) ConvertReturn(ReturnsSpec? returns, object? raw, bool ownsReference)
        {
            var type = returns?.Type?.ToLowerInvariant() ?? "void";
            switch (type)
            {
                case "void":
                    if (ownsReference)
                    {
                        ComLifetime.Release(raw);
                    }

                    return (null, null);

                case "bool":
                    return (raw is bool bb ? bb : raw != null && Convert.ToBoolean(raw), null);

                case "number":
                    if (raw is null or IConvertible)
                    {
                        return (raw, null);
                    }

                    return Reject(raw, type, ownsReference);

                case "string":
                    return (raw as string, null);

                case "feature":
                    return (ResultConverters.ToFeatureRef(raw, ownsReference), null);

                case "sketchsegment":
                    return (ResultConverters.ToSketchSegmentRef(raw, ownsReference), null);

                case "sketchsegments":
                    return (ResultConverters.ToSketchSegmentRefs(ToObjectEnumerable(raw), ownsReference), null);

                case "document":
                    return ToDocumentDto(raw, ownsReference);

                default:
                    // Unknown/unhandled returns.type — refuse rather than pass
                    // the raw value through, per the code review's explicit
                    // instruction: this must be a failed step, not a
                    // success with an "unconvertible" marker payload.
                    return Reject(raw, type, ownsReference, unknownType: true);
            }
        }

        // A live document object (e.g. IModelDocExtension.Document, the same
        // COM identity as SwDocument.Model) converted to the same {title, path,
        // type} shape RunNewPart returns for a freshly created document. Reads
        // are done via strictly read-only property/method calls before any
        // release, so this is safe even when ownsReference is false and the
        // object is never released at all.
        private static (object? Value, string? Error) ToDocumentDto(object? raw, bool ownsReference)
        {
            if (raw == null)
            {
                return (null, null);
            }

            try
            {
                var hasTitle = ComPropertyReader.TryGetMember(raw, "GetTitle", null, out var titleValue);
                var hasPath = ComPropertyReader.TryGetMember(raw, "GetPathName", null, out var pathValue);
                var hasType = ComPropertyReader.TryGetProperty(raw, "GetType", out var typeValue);

                if (!hasTitle || titleValue is not string title)
                {
                    // Release happens in the 'finally' below (based on the
                    // caller's ownsReference), not here — avoid a double release.
                    return (null, "Recipe declares returns.type 'document', but the returned object's Title could not be read (it may not be a document at all).");
                }

                // swDocumentTypes_e: 1 = Part, 2 = Assembly, 3 = Drawing (matches
                // SwBridge's SwDocumentType enum ordering; duplicated here rather
                // than referencing SolidWorks.Interop.swconst directly, which
                // swmcp deliberately never does).
                var typeName = hasType && typeValue is int t
                    ? t switch { 1 => "Part", 2 => "Assembly", 3 => "Drawing", _ => "Unknown" }
                    : "Unknown";

                var dto = new
                {
                    title,
                    path = hasPath ? pathValue as string ?? "" : "",
                    type = typeName,
                };

                return (dto, null);
            }
            finally
            {
                if (ownsReference)
                {
                    ComLifetime.Release(raw);
                }
            }
        }

        // Never let a live interface pointer past this line (ADR 0003). Used
        // both for a declared-but-unconvertible "number" and for any
        // unrecognised returns.type string.
        private static (object? Value, string? Error) Reject(object? raw, string declaredType, bool ownsReference, bool unknownType = false)
        {
            var comType = raw?.GetType().Name ?? "null";
            if (ownsReference)
            {
                ComLifetime.Release(raw);
            }

            var reason = unknownType
                ? $"declares an unrecognised returns.type '{declaredType}'"
                : $"declares returns.type '{declaredType}', but the call returned a '{comType}' that cannot be safely converted to it";

            return (null, $"Refusing to return an unconvertible value: the recipe {reason}. Fix the recipe's 'returns' field.");
        }

        private static IEnumerable<object?>? ToObjectEnumerable(object? raw) => raw switch
        {
            null => null,
            object[] arr => arr,
            System.Collections.IEnumerable en => en.Cast<object?>(),
            _ => null,
        };

        // ----------------------------------------------------------- helpers

        private static OperationResult Ok(object? ret, SwDocument? doc, IReadOnlyDictionary<string, object?>? boundArgs = null) =>
            new(true, null, ret, Snapshot(doc), boundArgs);

        private static OperationResult Fail(string error, SwDocument? doc = null, IReadOnlyDictionary<string, object?>? boundArgs = null) =>
            new(false, error, null, Snapshot(doc), boundArgs);

        // M5: the error path itself used to do unguarded COM work — a document
        // that a step just closed/invalidated (or a shared RCW disconnected by
        // an earlier converter bug, see H4) would throw here, replacing the
        // carefully-worded ADR 0002 failure report with an opaque exception at
        // exactly the moment the user most needs to know their document state.
        // The snapshot is diagnostic only; never let it replace the real error.
        private static DocumentStateSnapshot? Snapshot(SwDocument? doc)
        {
            if (doc == null)
            {
                return null;
            }

            try
            {
                return new DocumentStateSnapshot(
                    doc.Info.Title,
                    DocumentStateProbes.IsInSketchMode(doc.Model),
                    DocumentStateProbes.GetFeatureCount(doc.Model),
                    DocumentStateProbes.GetSelectionCount(doc.Model));
            }
            catch (Exception ex) when (ex is SwBridgeException or COMException or InvalidComObjectException)
            {
                return new DocumentStateSnapshot($"(unreadable: {ex.GetType().Name})", false, -1, -1);
            }
        }

        // H5: guarded so error-message construction can never itself throw and
        // replace a clear refusal (e.g. "documentName is required") with an
        // opaque exception — the exact scenario when SolidWorks closes between
        // the precondition check and this call.
        private string DescribeOpenDocuments()
        {
            try
            {
                return string.Join(", ", _documents.ListOpenDocuments().Select(d => $"{d.Title} ({d.Type})"));
            }
            catch (Exception ex) when (ex is SwBridgeException or COMException)
            {
                return $"(could not list open documents: {ex.Message})";
            }
        }
    }
}
