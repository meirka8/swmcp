using System.Text.Json;
using SwBridge;
using swmcp.server.Models;

namespace swmcp.server.Services
{
    /// <summary>
    /// The operation registry: name → <see cref="OperationRecipe"/>. Ships a
    /// seed (<c>known_operations.json</c>, next to the exe) and persists
    /// user-registered recipes to <c>%LOCALAPPDATA%\swmcp\known_operations.json</c>
    /// — mirroring <c>SchemaManager</c>, but deliberately <b>not</b> mirroring
    /// its known gotcha: the seed is never copied into the persisted store, so
    /// it is re-read fresh from the shipped file on every start, while
    /// registered entries are never touched by that refresh (ADR 0001 §1,
    /// "source").
    /// </summary>
    public class OperationManager
    {
        public const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        private static readonly HashSet<string> ValidScopes = new(StringComparer.OrdinalIgnoreCase) { "application", "document" };
        private static readonly HashSet<string> ValidKinds = new(StringComparer.OrdinalIgnoreCase) { "method", "propertySet", "propertyGet" };
        private static readonly HashSet<string> ValidParamTypes = new(StringComparer.OrdinalIgnoreCase)
        { "bool", "int", "double", "string", "length", "angle", "enum", "comNull" };
        private static readonly HashSet<string> ValidRequireChecks = new(StringComparer.OrdinalIgnoreCase)
        { "documentType", "inSketchMode", "notInSketchMode", "selectionCount", "selectionType" };
        private static readonly HashSet<string> ValidVerifyChecks = new(StringComparer.OrdinalIgnoreCase)
        { "returnNotNull", "returnTrue", "featureCountIncreased", "sketchSegmentCountIncreased", "sketchModeIs", "noNewRebuildErrors" };
        private static readonly HashSet<string> ValidReturnTypes = new(StringComparer.OrdinalIgnoreCase)
        { "void", "bool", "number", "string", "feature", "sketchSegment", "sketchSegments", "document" };

        private readonly string _seedPath;
        private readonly string _registeredPath;
        private readonly SwConnection _connection;
        private readonly DocumentManager _documents;

        private Dictionary<string, OperationRecipe> _seed = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, OperationRecipe> _registered = new(StringComparer.OrdinalIgnoreCase);

        public OperationManager(SwConnection connection, DocumentManager documents)
        {
            _connection = connection;
            _documents = documents;

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "swmcp");
            Directory.CreateDirectory(appDataPath);
            _registeredPath = Path.Combine(appDataPath, "known_operations.json");
            _seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "known_operations.json");

            ReloadSeed();
            LoadRegistered();
        }

        /// <summary>Re-reads the shipped seed file. Called on construction; every server start re-refreshes the seed.</summary>
        public void ReloadSeed() => _seed = LoadFile(_seedPath, "seed");

        private void LoadRegistered() =>
            _registered = File.Exists(_registeredPath)
                ? LoadFile(_registeredPath, "registered")
                : new Dictionary<string, OperationRecipe>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, OperationRecipe> LoadFile(string path, string source)
        {
            var result = new Dictionary<string, OperationRecipe>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return result;
            }

            try
            {
                var file = JsonSerializer.Deserialize<OperationFile>(File.ReadAllText(path), JsonOptions);
                if (file?.Operations == null)
                {
                    return result;
                }

                foreach (var op in file.Operations)
                {
                    if (string.IsNullOrWhiteSpace(op.Name))
                    {
                        continue;
                    }

                    op.Source = source;
                    result[op.Name] = op;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load operations from '{path}': {ex.Message}");
            }

            return result;
        }

        /// <summary>Looks up an operation by name; registered entries shadow seed entries of the same name.</summary>
        public OperationRecipe? Get(string name) =>
            _registered.TryGetValue(name, out var registered) ? registered :
            _seed.TryGetValue(name, out var seeded) ? seeded : null;

        /// <summary>All operations, registered entries shadowing seed entries of the same name, sorted by name.</summary>
        public IReadOnlyList<OperationRecipe> List()
        {
            var merged = new Dictionary<string, OperationRecipe>(_seed, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, recipe) in _registered)
            {
                merged[name] = recipe;
            }

            return merged.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Validates a recipe's shape against the closed v1 vocabulary. Never touches SolidWorks.</summary>
        public (bool Ok, string? Error, List<string> Warnings) Validate(OperationRecipe recipe)
        {
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(recipe.Name))
            {
                return (false, "Recipe needs a non-empty 'name'.", warnings);
            }

            if (string.IsNullOrWhiteSpace(recipe.Member))
            {
                return (false, "Recipe needs a non-empty 'member'.", warnings);
            }

            if (!ValidScopes.Contains(recipe.Scope))
            {
                return (false, $"'scope' must be one of: {string.Join(", ", ValidScopes)}.", warnings);
            }

            if (!ValidKinds.Contains(recipe.Kind))
            {
                return (false, $"'kind' must be one of: {string.Join(", ", ValidKinds)}.", warnings);
            }

            if (recipe.Returns != null && !ValidReturnTypes.Contains(recipe.Returns.Type))
            {
                return (false, $"'returns.type' must be one of: {string.Join(", ", ValidReturnTypes)}.", warnings);
            }

            var seenParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in recipe.Params)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                {
                    return (false, "Every param needs a non-empty 'name'.", warnings);
                }

                if (!seenParams.Add(p.Name))
                {
                    return (false, $"Duplicate param name '{p.Name}'.", warnings);
                }

                if (!ValidParamTypes.Contains(p.Type))
                {
                    return (false, $"Param '{p.Name}': type must be one of: {string.Join(", ", ValidParamTypes)}.", warnings);
                }
            }

            foreach (var r in recipe.Requires)
            {
                if (!ValidRequireChecks.Contains(r.Check))
                {
                    return (false, $"Unknown 'requires' check '{r.Check}'. Known: {string.Join(", ", ValidRequireChecks)}.", warnings);
                }
            }

            foreach (var v in recipe.Verify)
            {
                if (!ValidVerifyChecks.Contains(v.Check))
                {
                    return (false, $"Unknown 'verify' check '{v.Check}'. Known: {string.Join(", ", ValidVerifyChecks)}.", warnings);
                }
            }

            if (recipe.Verify.Count == 0)
            {
                warnings.Add(
                    "Recipe has no 'verify' entries. Per ADR 0002 this is a smell: SolidWorks write APIs frequently " +
                    "report failure by returning Nothing/False rather than throwing, so an unverified write is " +
                    "indistinguishable from a no-op.");
            }

            return (true, null, warnings);
        }

        /// <summary>
        /// Validates, best-effort live-checks against the COM type library when
        /// SolidWorks is reachable, and persists. Never rejects on a live-check
        /// mismatch — only warns (ADR 0001 §4).
        /// </summary>
        public (bool Ok, string? Error, List<string> Warnings) Register(OperationRecipe recipe)
        {
            var (ok, error, warnings) = Validate(recipe);
            if (!ok)
            {
                return (false, error, warnings);
            }

            try
            {
                warnings.AddRange(_connection.Dispatcher.Run(() => LiveCheck(recipe)));
            }
            catch (Exception ex)
            {
                warnings.Add($"Live arity/name check could not run: {ex.Message}");
            }

            if (_seed.ContainsKey(recipe.Name))
            {
                warnings.Add($"This name shadows seed operation '{recipe.Name}' — the registered version is used from now on.");
            }

            recipe.Source = "registered";
            _registered[recipe.Name] = recipe;
            Save();

            return (true, null, warnings);
        }

        // Runs on the SwDispatcher thread (via Register's Dispatcher.Run call).
        private List<string> LiveCheck(OperationRecipe recipe)
        {
            var warnings = new List<string>();

            if (!_connection.IsConnected)
            {
                warnings.Add("SolidWorks is not reachable — skipped the live arity/name check against the COM type library.");
                return warnings;
            }

            object? root;
            if (string.Equals(recipe.Scope, "application", StringComparison.OrdinalIgnoreCase))
            {
                root = _connection.GetApp();
            }
            else
            {
                // Best-effort only: register_operation has no documentName, so
                // this uses whatever document happens to be open, if any.
                var candidate = _documents.GetOpenDocuments().FirstOrDefault();
                if (candidate == null)
                {
                    warnings.Add(
                        "Recipe is document-scoped and no document is currently open, so the live check could not " +
                        "resolve a target path. It will be checked the first time run_operation targets a real document.");
                    return warnings;
                }

                root = candidate.Model;
            }

            try
            {
                var pathResult = ComPath.Resolve(root, recipe.Target ?? "");
                if (!pathResult.Success)
                {
                    warnings.Add($"Live check: could not resolve target '{recipe.Target}' ({pathResult.FailureDetail}).");
                    return warnings;
                }

                var members = ComTypeInspector.DescribeMembers(pathResult.Value);
                if (members.Count == 0)
                {
                    members = ComTypeInspector.DescribeMembersViaInterop(pathResult.Value);
                }

                var match = members.FirstOrDefault(m => string.Equals(m.Name, recipe.Member, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    warnings.Add(
                        $"Live check: member '{recipe.Member}' was not found on the resolved target via ITypeInfo/interop " +
                        "discovery. This can be a false positive (dispatch aliases, no discoverable type info) — " +
                        "registration proceeds anyway.");
                }
                else if (recipe.Kind.Equals("method", StringComparison.OrdinalIgnoreCase) && match.ParamCount != recipe.Params.Count)
                {
                    warnings.Add(
                        $"Live check: '{recipe.Member}' reports {match.ParamCount} parameter(s) in the type library, " +
                        $"the recipe declares {recipe.Params.Count}. This can be a false positive (optional params) — " +
                        "registration proceeds anyway.");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Live check threw {ex.GetType().Name}: {ex.Message} — registration proceeds anyway.");
            }

            return warnings;
        }

        private void Save()
        {
            try
            {
                var file = new OperationFile { SchemaVersion = CurrentSchemaVersion, Operations = _registered.Values.ToList() };
                File.WriteAllText(_registeredPath, JsonSerializer.Serialize(file, JsonOptions));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to save registered operations: {ex.Message}");
            }
        }
    }
}
