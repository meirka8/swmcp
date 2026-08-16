using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SwBridge;
using swmcp.server.Models;
using swmcp.server.Services;

namespace swmcp.server.Tools
{
    /// <summary>One step of a <see cref="OperationsTool.RunOperations"/> batch.</summary>
    public sealed class OperationStepInput
    {
        [Description("Operation name, e.g. 'insert_sketch'.")]
        public string Operation { get; set; } = "";

        [Description("Named arguments for this step's operation. See describe_operation for the operation's declared params.")]
        public Dictionary<string, JsonElement>? Args { get; set; }
    }

    /// <summary>
    /// The generic write-operation surface (ADR 0001): seven tools instead of a
    /// per-feature tool per SolidWorks capability. Every document-scoped
    /// operation requires an explicit <c>documentName</c> — stricter than the
    /// read tools in <see cref="SolidWorksTool"/>, deliberately: a wrong read
    /// is a wrong answer, a wrong write modifies the wrong part.
    /// </summary>
    [McpServerToolType]
    public class OperationsTool
    {
        private readonly OperationManager _operations;
        private readonly OperationRunner _runner;
        private readonly DocumentManager _documents;
        private readonly SwConnection _connection;

        public OperationsTool(OperationManager operations, OperationRunner runner, DocumentManager documents, SwConnection connection)
        {
            _operations = operations;
            _runner = runner;
            _documents = documents;
            _connection = connection;
        }

        [McpServerTool, Description(
            "Lists every registered SolidWorks write operation: name, one-line summary, scope (application/document), " +
            "and provenance (seed = shipped with the server, registered = added at runtime via register_operation). " +
            "Cheap — call this first, then describe_operation for the ones you intend to call.")]
        public object ListOperations()
        {
            return new
            {
                operations = _operations.List().Select(o => new { o.Name, o.Summary, o.Scope, o.Source }),
            };
        }

        [McpServerTool, Description(
            "Returns the full recipe for one operation: every named parameter (type, unit, default, required), " +
            "declared preconditions, the return shape, and the post-condition checks that decide success. Read this " +
            "before calling run_operation with an operation you have not used yet — parameter names and units are " +
            "not guessable from the summary alone. length/angle params always require an explicit unit (e.g. 'depth1' " +
            "on extrude_boss accepts '5 mm' or '0.005 m', never a bare number).")]
        public object DescribeOperation([Description("Operation name, e.g. 'extrude_boss'.")] string operation)
        {
            var recipe = _operations.Get(operation);
            if (recipe == null)
            {
                return new { error = $"No operation named '{operation}'. Call list_operations to see available operations." };
            }

            return recipe;
        }

        [McpServerTool, Description(
            "Executes one operation recipe against one document. documentName is REQUIRED for every document-scoped " +
            "operation (there is no active-document fallback on the write path) — omit it only for application-scoped " +
            "operations such as new_part. Unknown argument names are REFUSED (not silently ignored) naming the typo and " +
            "the recipe's real param list. A failed precondition is refused, never silently satisfied: the error names " +
            "which operation to call first. A step that invokes without a COM error but whose declared post-conditions " +
            "do not hold is still reported as a failure (ADR 0002) — the document is left exactly as it was; call the " +
            "'undo' operation yourself if you need to back out, nothing does that automatically. The response's " +
            "'boundArgs' field echoes the exact SI values actually sent to COM (after unit parsing) — check it whenever " +
            "the geometry looks wrong; it is the audit trail for a bad binding.")]
        public object RunOperation(
            [Description(
                "Operation name, e.g. 'insert_sketch'.")]
            string operation,
            [Description(
                "Named arguments for the operation's declared params (see describe_operation). length/angle params " +
                "REQUIRE an explicit unit — a quantity string like '5 mm' or '30 deg', or an explicit SI string like " +
                "'0.005 m' or '0.5 rad'; a bare number is refused. Omitted params use their declared default. Any key " +
                "that does not name a declared param is refused, listing the recipe's real param names.")]
            Dictionary<string, JsonElement>? args = null,
            [Description(
                "Which open document to act on (title, file name, or path). Required for every document-scoped " +
                "operation; omit only for application-scoped operations (currently just new_part). A name matching " +
                "more than one open document is refused rather than guessed.")]
            string? documentName = null)
        {
            var recipe = _operations.Get(operation);
            if (recipe == null)
            {
                return new { error = $"No operation named '{operation}'. Call list_operations to see available operations." };
            }

            try
            {
                return ToResponse(_runner.Run(recipe, documentName, args));
            }
            catch (Exception ex) when (ex is SwBridgeException or ObjectDisposedException)
            {
                // H5: SwBridgeException covers SwNotRunningException (SolidWorks
                // closed) and SwDispatchTimeoutException (a modal dialog or a
                // very long rebuild blocked the whole call) — either would
                // otherwise surface as a generic JSON-RPC error instead of the
                // structured {success:false} shape every other failure uses.
                return new { success = false, error = $"'{operation}' could not run: {ex.Message}" };
            }
        }

        [McpServerTool, Description(
            "Executes an ordered batch of operations against one document, failing fast, as a SINGLE unit of work on " +
            "SolidWorks' dispatcher (no other request — read or write — can interleave mid-batch and mutate the active " +
            "sketch or selection a later step depends on). Operation names are resolved before any step runs: an " +
            "unknown operation anywhere in the list refuses the whole batch up front, with nothing executed. On the " +
            "first step that fails (a refused precondition, a COM invocation failure, or a verification failure), " +
            "execution stops and the response reports every step completed so far plus the failing step's index, " +
            "operation name, error, boundArgs, and the document's state at that point. There is NO automatic rollback " +
            "(ADR 0002) — a partial plan leaves the document exactly as the completed steps left it; call the 'undo' " +
            "operation deliberately if you need to back out. Steps do not pass return values to each other; coupling " +
            "goes entirely through SolidWorks' own state (active sketch, current selection). The whole batch shares one " +
            "generous timeout (120s + 30s per step) — if the WHOLE batch does not complete within it (e.g. a modal " +
            "SolidWorks dialog appears mid-batch), the call fails with no transcript at all, since the in-progress work " +
            "cannot be recovered from a timed-out wait; this is rare with the generous default and is the accepted " +
            "trade-off for single-dispatch batch isolation. If the plan needs a new document, call run_operation with " +
            "'new_part' first and pass its returned title as documentName here — new_part itself is application-scoped " +
            "and cannot be a step in this batch.")]
        public object RunOperations(
            [Description("Ordered steps to execute against documentName, in order.")] OperationStepInput[] steps,
            [Description("Which open document every document-scoped step acts on.")] string? documentName = null)
        {
            var resolvedSteps = new List<(OperationRecipe Recipe, IReadOnlyDictionary<string, JsonElement>? Args)>();
            for (var i = 0; i < steps.Length; i++)
            {
                var recipe = _operations.Get(steps[i].Operation);
                if (recipe == null)
                {
                    return new
                    {
                        error = $"Step {i}: no operation named '{steps[i].Operation}'. Call list_operations to see available operations. " +
                                "No step in this batch ran (names are resolved before dispatch).",
                        failedStepIndex = i,
                        failedOperation = steps[i].Operation,
                        completedSteps = Array.Empty<object>(),
                    };
                }

                resolvedSteps.Add((recipe, steps[i].Args));
            }

            try
            {
                var timeout = TimeSpan.FromSeconds(120 + (30 * Math.Max(1, steps.Length)));
                var results = _runner.RunBatch(resolvedSteps, documentName, timeout);

                var completed = new List<object>();
                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    if (!result.Success)
                    {
                        return new
                        {
                            error = $"Step {i} ('{steps[i].Operation}') failed: {result.Error}",
                            failedStepIndex = i,
                            failedOperation = steps[i].Operation,
                            documentState = result.DocumentState,
                            boundArgs = result.BoundArgs,
                            completedSteps = completed,
                        };
                    }

                    completed.Add(new { index = i, operation = steps[i].Operation, result = ToResponse(result) });
                }

                return new { completedSteps = completed };
            }
            catch (Exception ex) when (ex is SwBridgeException or ObjectDisposedException)
            {
                // H5: the whole batch is one dispatched unit of work (see the
                // tool description) — if the dispatch itself faults or times
                // out before returning, there is no partial 'results' list to
                // report (the in-progress work is still running on the
                // dispatcher thread and cannot be recovered from here). Report
                // that plainly rather than letting a generic JSON-RPC error
                // through.
                return new
                {
                    error = $"Batch could not run: {ex.Message}",
                    completedSteps = Array.Empty<object>(),
                };
            }
        }

        [McpServerTool, Description(
            "Validates and persists a new operation recipe — the enrichment entry point for any SolidWorks capability " +
            "beyond the shipped seed. Recommended loop: call describe_com_members to find real member names/signatures " +
            "on the target you want to drive, cross-reference SolidWorks API documentation for parameter meaning/units/" +
            "enum values, then call this. Validates recipe shape (known scope/kind/param-type/requires/verify " +
            "vocabulary, unique param names, application-scoped recipes cannot declare 'requires') and, when SolidWorks " +
            "is reachable, best-effort checks the target path and member name/arity against the live COM type library " +
            "— WARNING, never rejecting, on a mismatch, since dispatch aliases and optional parameters make the type " +
            "library an imperfect oracle. Persists to %LOCALAPPDATA%\\swmcp\\known_operations.json and survives " +
            "restarts; a name that matches a seed operation shadows it from then on. Use unregister_operation to " +
            "remove a recipe registered here.")]
        public object RegisterOperation(
            [Description("The full recipe, in the same shape describe_operation returns (name, summary, scope, target, kind, member, requires, params, returns, verify).")]
            OperationRecipe recipe)
        {
            try
            {
                var (ok, error, warnings) = _operations.Register(recipe);
                return ok ? new { registered = recipe.Name, warnings } : new { error, warnings };
            }
            catch (Exception ex) when (ex is SwBridgeException or ObjectDisposedException)
            {
                return new { error = $"register_operation could not run: {ex.Message}" };
            }
        }

        [McpServerTool, Description(
            "Removes a recipe added via register_operation, persisting the change. Refuses (rather than doing nothing) " +
            "for a name that is not currently registered — including a seed operation's name, since seed recipes ship " +
            "with the server and are refreshed from known_operations.json on every start, so 'removing' one would just " +
            "have it reappear next launch. If a registered recipe shadowed a seed operation of the same name, " +
            "unregistering it restores the seed version.")]
        public object UnregisterOperation([Description("Name of a registered operation to remove.")] string operation)
        {
            var (ok, error) = _operations.Unregister(operation);
            return ok ? new { unregistered = operation } : new { error };
        }

        [McpServerTool, Description(
            "Read-only discovery of the members a live SolidWorks COM object actually exposes — the enrichment loop's " +
            "eyes. Point it at a dotted target path from a document (e.g. 'FeatureManager', 'Extension.SelectionManager', " +
            "'SketchManager', or '' for the document root itself) to see real method/property names and parameter " +
            "counts before writing a register_operation recipe, instead of guessing. Pass featureName instead of " +
            "targetPath to inspect a specific feature's definition object. Omit documentName to inspect application-" +
            "level (ISldWorks) targets instead of a document's. Discovery unions every mechanism SolidWorks exposes " +
            "(ITypeInfo plus an interop-assembly probe) rather than stopping at whichever answers first — this is what " +
            "makes members like EditRebuild3/SaveAs3/EditUndo2/ClearSelection2 findable on a document root (targetPath " +
            "''), which a narrower probe reports as ~175 members and misses all four of. Results are never silently " +
            "truncated: the response's 'totalCount' is always the true member count and 'returned'/'offset'/'hasMore' " +
            "say exactly what page you are looking at — this matters more here than most discovery tools, since a " +
            "document root can report upwards of 900 members. Use nameFilter (a case-insensitive substring, e.g. 'Ray') " +
            "to jump straight to a member you already suspect exists instead of paging through hundreds — this is how " +
            "you find something like Extension.SelectByRay even when it is far past the default page size.")]
        public object DescribeComMembers(
            [Description("Which open document to inspect. Omit to inspect the SolidWorks application object itself.")]
            string? documentName = null,
            [Description("Dotted path from the document (or application) root, e.g. 'FeatureManager' or 'Extension.SelectionManager'. Ignored when featureName is given. Empty/omitted means the root object itself.")]
            string? targetPath = null,
            [Description("Name of a feature (as shown in the tree), e.g. 'Boss-Extrude1', whose definition object's members to discover. Requires documentName. Takes precedence over targetPath.")]
            string? featureName = null,
            [Description("Case-insensitive substring filter on member name, e.g. 'Ray' to find SelectByRay. Applied before paging.")]
            string? nameFilter = null,
            [Description("Zero-based index into the (optionally filtered) member list to start returning from. Use with 'hasMore'/'totalCount' from a previous call to page through the rest.")]
            int offset = 0,
            [Description("Maximum members to return in this call. Default 200 — raise it or use nameFilter/offset for a target with more members than that.")]
            int limit = 200)
        {
            try
            {
                if (featureName != null)
                {
                    if (string.IsNullOrWhiteSpace(documentName))
                    {
                        return new { error = "documentName is required when featureName is given." };
                    }

                    var doc = _documents.Resolve(documentName);
                    if (doc == null)
                    {
                        return new { error = $"No open document matches '{documentName}'. Open documents: {DescribeOpenDocuments()}" };
                    }

                    var featureMembers = doc.DescribeFeatureDefinition(featureName);
                    if (featureMembers == null)
                    {
                        return new { error = $"Feature '{featureName}' was not found in '{doc.Info.Title}', or its definition object could not be read." };
                    }

                    return PageMembers($"{doc.Info.Title}!{featureName}", "featureDefinition", featureMembers, nameFilter, offset, limit);
                }

                object root;
                string rootDescription;
                if (documentName != null)
                {
                    var doc = _documents.Resolve(documentName);
                    if (doc == null)
                    {
                        return new { error = $"No open document matches '{documentName}'. Open documents: {DescribeOpenDocuments()}" };
                    }

                    root = doc.Model;
                    rootDescription = doc.Info.Title;
                }
                else
                {
                    root = _connection.GetApp();
                    rootDescription = "(application)";
                }

                return _connection.Dispatcher.Run<object>(() =>
                {
                    var path = targetPath ?? "";
                    var resolved = ComPath.Resolve(root, path);
                    if (!resolved.Success)
                    {
                        return new
                        {
                            error = $"Could not resolve '{path}' on {rootDescription} (failed at '{resolved.FailedSegment}': {resolved.FailureDetail}).",
                        };
                    }

                    // Gap #3 (UAT re-verdict): DescribeMembers-then-fallback-to-
                    // DescribeMembersViaInterop used to report a document root's
                    // ITypeInfo (~175 members via IProvideClassInfo) and stop
                    // there — the either/or fallback only kicks in when
                    // DescribeMembers finds NOTHING, but a document root finds
                    // plenty via ITypeInfo, just not everything. That default
                    // interface omits IModelDoc2 members like EditRebuild3,
                    // SaveAs3, EditUndo2, ClearSelection2 — the members behind
                    // four of this server's own seed operations — so discovery
                    // was blind to exactly the document-level surface a client
                    // would most want to enrich. DescribeAllMembers unions both
                    // paths (947 members on a document root, confirmed live)
                    // instead of picking whichever answers first. Deliberately
                    // unfiltered on the interop side (unlike
                    // ModelInspector.DescribeFeatureDefinition's *FeatureData*
                    // filter) — B4: filtering here is exactly what hid
                    // Extension.SelectByRay from discovery during the UAT.
                    // Paging (below) is how this stays usable instead of
                    // truncation, which matters even more at 947 members.
                    var members = ComTypeInspector.DescribeAllMembers(resolved.Value);
                    var target = $"{rootDescription}!{(string.IsNullOrEmpty(path) ? "(root)" : path)}";
                    return PageMembers(target, "ITypeInfo+interop (union)", members, nameFilter, offset, limit);
                });
            }
            catch (SwBridgeException ex)
            {
                return new { error = ex.Message };
            }
        }

        // B4: filter-then-page, and always report the true total — the UAT's
        // complaint was not "300 is too small," it was that truncation was
        // silent and undiscoverable (no filter, no way to page, no visible
        // count of what was hidden).
        private static object PageMembers(
            string target, string discoveredVia, IReadOnlyList<ComMemberInfo> members, string? nameFilter, int offset, int limit)
        {
            var filtered = string.IsNullOrWhiteSpace(nameFilter)
                ? members
                : members.Where(m => m.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            var safeOffset = Math.Max(0, offset);
            var safeLimit = Math.Max(0, limit);
            var page = filtered.Skip(safeOffset).Take(safeLimit).ToList();

            return new
            {
                target,
                discoveredVia,
                nameFilter,
                totalCount = filtered.Count,
                offset = safeOffset,
                returned = page.Count,
                hasMore = safeOffset + page.Count < filtered.Count,
                members = page,
            };
        }

        private static object ToResponse(OperationResult result) => new
        {
            success = result.Success,
            error = result.Error,
            @return = result.Return,
            documentState = result.DocumentState,
            boundArgs = result.BoundArgs,
        };

        // H5: guarded so error-message construction (e.g. "no document matches
        // X, open documents are: ...") can never itself throw and replace a
        // clear refusal with an opaque exception.
        private string DescribeOpenDocuments()
        {
            try
            {
                return string.Join(", ", _documents.ListOpenDocuments().Select(d => $"{d.Title} ({d.Type})"));
            }
            catch (Exception ex) when (ex is SwBridgeException)
            {
                return $"(could not list open documents: {ex.Message})";
            }
        }
    }
}
