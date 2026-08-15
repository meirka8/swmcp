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
    /// The generic write-operation surface (ADR 0001): six tools instead of a
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
            "not guessable from the summary alone (e.g. 'depth1' on extrude_boss is a length: a bare number in " +
            "meters, or a string like '5 mm').")]
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
            "operations such as new_part. A failed precondition is refused, never silently satisfied: the error names " +
            "which operation to call first. A step that invokes without a COM error but whose declared post-conditions " +
            "do not hold is still reported as a failure (ADR 0002) — the document is left exactly as it was; call the " +
            "'undo' operation yourself if you need to back out, nothing does that automatically.")]
        public object RunOperation(
            [Description("Operation name, e.g. 'insert_sketch'.")] string operation,
            [Description(
                "Named arguments for the operation's declared params (see describe_operation). Length params accept a " +
                "bare number (meters) or a quantity string like '5 mm'; angle params accept radians or e.g. '30 deg'. " +
                "Omitted params use their declared default.")]
            Dictionary<string, JsonElement>? args = null,
            [Description(
                "Which open document to act on (title, file name, or path). Required for every document-scoped " +
                "operation; omit only for application-scoped operations (currently just new_part).")]
            string? documentName = null)
        {
            var recipe = _operations.Get(operation);
            if (recipe == null)
            {
                return new { error = $"No operation named '{operation}'. Call list_operations to see available operations." };
            }

            return ToResponse(_runner.Run(recipe, documentName, args));
        }

        [McpServerTool, Description(
            "Executes an ordered batch of operations against one document, failing fast: on the first step that fails " +
            "(a refused precondition, a COM invocation failure, or a verification failure), execution stops immediately " +
            "and the response reports every step completed so far plus the failing step's index, operation name and " +
            "error, and the document's state at that point. There is NO automatic rollback (ADR 0002) — a partial plan " +
            "leaves the document exactly as the completed steps left it; call the 'undo' operation deliberately if you " +
            "need to back out. Steps do not pass return values to each other (no data flow); coupling between steps " +
            "goes entirely through SolidWorks' own state (active sketch, current selection). If the plan needs a new " +
            "document, call run_operation with 'new_part' first and pass its returned title as documentName here — " +
            "new_part itself is application-scoped and cannot be a step in this batch.")]
        public object RunOperations(
            [Description("Ordered steps to execute against documentName, in order.")] OperationStepInput[] steps,
            [Description("Which open document every document-scoped step acts on.")] string? documentName = null)
        {
            var completed = new List<object>();
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                var recipe = _operations.Get(step.Operation);
                if (recipe == null)
                {
                    return new
                    {
                        error = $"Step {i}: no operation named '{step.Operation}'. Call list_operations to see available operations.",
                        failedStepIndex = i,
                        failedOperation = step.Operation,
                        completedSteps = completed,
                    };
                }

                var result = _runner.Run(recipe, documentName, step.Args);
                if (!result.Success)
                {
                    return new
                    {
                        error = $"Step {i} ('{step.Operation}') failed: {result.Error}",
                        failedStepIndex = i,
                        failedOperation = step.Operation,
                        documentState = result.DocumentState,
                        completedSteps = completed,
                    };
                }

                completed.Add(new { index = i, operation = step.Operation, result = ToResponse(result) });
            }

            return new { completedSteps = completed };
        }

        [McpServerTool, Description(
            "Validates and persists a new operation recipe — the enrichment entry point for any SolidWorks capability " +
            "beyond the shipped seed. Recommended loop: call describe_com_members to find real member names/signatures " +
            "on the target you want to drive, cross-reference SolidWorks API documentation for parameter meaning/units/" +
            "enum values, then call this. Validates recipe shape (known scope/kind/param-type/requires/verify " +
            "vocabulary, unique param names) and, when SolidWorks is reachable, best-effort checks the target path and " +
            "member name/arity against the live COM type library — WARNING, never rejecting, on a mismatch, since " +
            "dispatch aliases and optional parameters make the type library an imperfect oracle. Persists to " +
            "%LOCALAPPDATA%\\swmcp\\known_operations.json and survives restarts; a name that matches a seed operation " +
            "shadows it from then on.")]
        public object RegisterOperation(
            [Description("The full recipe, in the same shape describe_operation returns (name, summary, scope, target, kind, member, requires, params, returns, verify).")]
            OperationRecipe recipe)
        {
            var (ok, error, warnings) = _operations.Register(recipe);
            if (!ok)
            {
                return new { error, warnings };
            }

            return new { registered = recipe.Name, warnings };
        }

        [McpServerTool, Description(
            "Read-only discovery of the members a live SolidWorks COM object actually exposes — the enrichment loop's " +
            "eyes. Point it at a dotted target path from a document (e.g. 'FeatureManager', 'Extension.SelectionManager', " +
            "'SketchManager') to see real method/property names and parameter counts before writing a register_operation " +
            "recipe, instead of guessing. Pass featureName instead of targetPath to inspect a specific feature's " +
            "definition object (e.g. to find the accessor for a value get_part_info does not yet know how to read) — " +
            "this is the same discovery register_feature_schema authors use. Omit documentName to inspect application-" +
            "level (ISldWorks) targets instead of a document's.")]
        public object DescribeComMembers(
            [Description("Which open document to inspect. Omit to inspect the SolidWorks application object itself.")]
            string? documentName = null,
            [Description("Dotted path from the document (or application) root, e.g. 'FeatureManager' or 'Extension.SelectionManager'. Ignored when featureName is given. Empty/omitted means the root object itself.")]
            string? targetPath = null,
            [Description("Name of a feature (as shown in the tree), e.g. 'Boss-Extrude1', whose definition object's members to discover. Requires documentName. Takes precedence over targetPath.")]
            string? featureName = null)
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

                    return new { target = $"{doc.Info.Title}!{featureName}", memberCount = featureMembers.Count, members = featureMembers };
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

                    var members = ComTypeInspector.DescribeMembers(resolved.Value);
                    var discoveredVia = "ITypeInfo";
                    if (members.Count == 0)
                    {
                        members = ComTypeInspector.DescribeMembersViaInterop(resolved.Value);
                        discoveredVia = "interop-assembly probe";
                    }

                    const int cap = 300;
                    var truncated = members.Count > cap;
                    var page = truncated ? members.Take(cap).ToList() : members;

                    return new
                    {
                        target = $"{rootDescription}!{(string.IsNullOrEmpty(path) ? "(root)" : path)}",
                        discoveredVia,
                        memberCount = members.Count,
                        truncated,
                        truncatedNote = truncated ? $"Showing the first {cap} of {members.Count} members." : null,
                        members = page,
                    };
                });
            }
            catch (SwNotRunningException ex)
            {
                return new { error = ex.Message };
            }
        }

        private static object ToResponse(OperationResult result) => new
        {
            success = result.Success,
            error = result.Error,
            @return = result.Return,
            documentState = result.DocumentState,
        };

        private string DescribeOpenDocuments() =>
            string.Join(", ", _documents.ListOpenDocuments().Select(d => $"{d.Title} ({d.Type})"));
    }
}
