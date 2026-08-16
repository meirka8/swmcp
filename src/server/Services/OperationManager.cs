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
        { "returnNotNull", "returnTrue", "returnEquals", "featureCountIncreased", "sketchSegmentCountIncreased", "sketchModeIs", "noNewRebuildErrors" };
        private static readonly HashSet<string> ValidReturnTypes = new(StringComparer.OrdinalIgnoreCase)
        { "void", "bool", "number", "string", "feature", "sketchSegment", "sketchSegments", "document" };

        private readonly string _seedPath;
        private readonly string _registeredPath;
        private readonly SwConnection _connection;
        private readonly DocumentManager _documents;

        // M3: both dictionaries are read from request threads (list_operations,
        // describe_operation, run_operation) and, for _registered, written from
        // a request thread too (register_operation/unregister_operation) — the
        // MCP SDK does not guarantee serial handler execution. A copy-on-write
        // swap under a lock (never an in-place '_registered[name] = recipe')
        // means readers always see a complete, un-torn dictionary — either the
        // version before the write or the version after, never a mid-resize
        // state. 'volatile' on the field ensures a reader on another thread
        // observes the new reference promptly after the swap.
        private readonly object _writeLock = new();
        private volatile Dictionary<string, OperationRecipe> _seed = new(StringComparer.OrdinalIgnoreCase);
        private volatile Dictionary<string, OperationRecipe> _registered = new(StringComparer.OrdinalIgnoreCase);

        // H2: true when the registered-operations store existed but could not
        // be parsed and was quarantined rather than silently treated as empty.
        // Surfaced as a warning on every subsequent register_operation call so
        // the loss is visible somewhere the user will see it (stderr on a
        // stdio server is not).
        private volatile bool _registeredStoreUnreadable;

        public OperationManager(SwConnection connection, DocumentManager documents)
            : this(connection, documents, DefaultSeedPath(), DefaultRegisteredPath())
        {
        }

        // Internal (not private) so swmcp.server.tests can point a manager at a
        // temp directory and exercise the quarantine/atomic-write path (H2)
        // without touching the real %LOCALAPPDATA%\swmcp store.
        internal OperationManager(SwConnection connection, DocumentManager documents, string seedPath, string registeredPath)
        {
            _connection = connection;
            _documents = documents;
            _seedPath = seedPath;
            _registeredPath = registeredPath;

            var directory = Path.GetDirectoryName(registeredPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ReloadSeed();
            LoadRegistered();
        }

        private static string DefaultRegisteredPath()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "swmcp");
            Directory.CreateDirectory(appDataPath);
            return Path.Combine(appDataPath, "known_operations.json");
        }

        private static string DefaultSeedPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "known_operations.json");

        /// <summary>Re-reads the shipped seed file. Called on construction; every server start re-refreshes the seed.</summary>
        public void ReloadSeed() => _seed = LoadFileBestEffort(_seedPath, "seed");

        // H2: previously caught every exception, logged one stderr line
        // (invisible on a stdio server), and returned an EMPTY dictionary —
        // which Save() would then happily persist over the original file on
        // the very next registration, permanently destroying every previously
        // registered recipe. Now: a missing file is legitimately empty (first
        // run); a present-but-unparseable file is quarantined (renamed with a
        // timestamp) rather than overwritten, and _registeredStoreUnreadable
        // is surfaced so the loss is visible to the user, not just stderr.
        private void LoadRegistered()
        {
            if (!File.Exists(_registeredPath))
            {
                _registered = new Dictionary<string, OperationRecipe>(StringComparer.OrdinalIgnoreCase);
                _registeredStoreUnreadable = false;
                return;
            }

            try
            {
                _registered = LoadFileOrThrow(_registeredPath, "registered");
                _registeredStoreUnreadable = false;
            }
            catch (Exception ex)
            {
                var quarantine = $"{_registeredPath}.bad-{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Move(_registeredPath, quarantine, overwrite: false);
                    Console.Error.WriteLine(
                        $"known_operations.json (registered store) was unreadable ({ex.Message}); moved to '{quarantine}'. " +
                        "Registered operations are unavailable this session; re-register them, or restore the quarantined " +
                        "file by hand once it is fixed.");
                }
                catch (Exception moveEx)
                {
                    Console.Error.WriteLine(
                        $"known_operations.json (registered store) was unreadable ({ex.Message}) and could not be " +
                        $"quarantined ({moveEx.Message}). Registered operations are unavailable this session, and the " +
                        "corrupt file was left in place rather than risk overwriting it.");
                }

                _registered = new Dictionary<string, OperationRecipe>(StringComparer.OrdinalIgnoreCase);
                _registeredStoreUnreadable = true;
            }
        }

        // Used for the seed, where "unreadable" degrades to "empty seed" with
        // a stderr line — acceptable because the seed is never the only copy
        // of anything (it ships in source control) and re-refreshes every start.
        private static Dictionary<string, OperationRecipe> LoadFileBestEffort(string path, string source)
        {
            try
            {
                return File.Exists(path)
                    ? LoadFileOrThrow(path, source)
                    : new Dictionary<string, OperationRecipe>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load operations from '{path}': {ex.Message}");
                return new Dictionary<string, OperationRecipe>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static Dictionary<string, OperationRecipe> LoadFileOrThrow(string path, string source)
        {
            var result = new Dictionary<string, OperationRecipe>(StringComparer.OrdinalIgnoreCase);
            var file = JsonSerializer.Deserialize<OperationFile>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException($"'{path}' did not deserialize to a valid operations file.");

            foreach (var op in file.Operations)
            {
                if (string.IsNullOrWhiteSpace(op.Name))
                {
                    continue;
                }

                op.Source = source;
                result[op.Name] = op;
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
            // Snapshot both volatile fields once each — each read is internally
            // consistent (never a torn dictionary, per the class remarks above);
            // the two reads racing a concurrent Register() at worst merges an
            // old-seed/new-registered (or vice versa) pairing for one call,
            // which is harmless since seed and registered are disjoint stores
            // merged only for display.
            var seed = _seed;
            var registered = _registered;

            var merged = new Dictionary<string, OperationRecipe>(seed, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, recipe) in registered)
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

            // M4: the v1 precondition vocabulary is document-scoped only
            // (documentType, inSketchMode/notInSketchMode, selectionCount,
            // selectionType all read a ModelDoc2). An application-scoped
            // recipe declaring 'requires' used to have every precondition
            // silently skipped at run time (CheckRequires was only ever called
            // when a document had been resolved) — rejected here instead, at
            // the moment a bad recipe would first be persisted, rather than
            // left to fail closed (or not at all) on every subsequent run.
            if (recipe.Requires.Count > 0 && string.Equals(recipe.Scope, "application", StringComparison.OrdinalIgnoreCase))
            {
                return (false,
                    "Application-scoped recipes cannot declare 'requires' — every v1 precondition check " +
                    "(documentType, inSketchMode, notInSketchMode, selectionCount, selectionType) is document-scoped only.",
                    warnings);
            }

            foreach (var r in recipe.Requires)
            {
                if (!ValidRequireChecks.Contains(r.Check))
                {
                    return (false, $"Unknown 'requires' check '{r.Check}'. Known: {string.Join(", ", ValidRequireChecks)}.", warnings);
                }

                if (string.Equals(r.Check, "selectionType", StringComparison.OrdinalIgnoreCase) && r.Mark == null)
                {
                    return (false, "'requires' check 'selectionType' needs 'mark'.", warnings);
                }
            }

            foreach (var v in recipe.Verify)
            {
                if (!ValidVerifyChecks.Contains(v.Check))
                {
                    return (false, $"Unknown 'verify' check '{v.Check}'. Known: {string.Join(", ", ValidVerifyChecks)}.", warnings);
                }

                if (string.Equals(v.Check, "returnEquals", StringComparison.OrdinalIgnoreCase) && v.Expected == null)
                {
                    return (false, "'verify' check 'returnEquals' needs 'expected'.", warnings);
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

            if (_registeredStoreUnreadable)
            {
                warnings.Add(
                    "The registered-operations store was unreadable and quarantined earlier this session (see server " +
                    "stderr for the quarantine path) — every previously registered recipe except this one is " +
                    "unavailable until the quarantined file is restored by hand.");
            }

            recipe.Source = "registered";

            lock (_writeLock)
            {
                var next = new Dictionary<string, OperationRecipe>(_registered, StringComparer.OrdinalIgnoreCase)
                {
                    [recipe.Name] = recipe,
                };
                Save(next);
                _registered = next;
            }

            return (true, null, warnings);
        }

        /// <summary>
        /// Removes a registered recipe by name. Refuses (rather than doing
        /// nothing silently) for a name that is not currently registered —
        /// including a pure-seed name, which was never reachable through this
        /// method at all: seed recipes ship with the server and are refreshed
        /// from disk every start, so "removing" one would just have it
        /// reappear next launch, which is worse than refusing outright.
        /// </summary>
        public (bool Ok, string? Error) Unregister(string name)
        {
            lock (_writeLock)
            {
                if (!_registered.ContainsKey(name))
                {
                    var reason = _seed.ContainsKey(name)
                        ? $"'{name}' is a seed operation — it ships with the server and cannot be unregistered. " +
                          "If you registered a recipe under this name to override it, that override is already gone " +
                          "(nothing is currently registered under this name)."
                        : $"No registered operation named '{name}'. Call list_operations to see what is currently registered.";
                    return (false, reason);
                }

                var next = new Dictionary<string, OperationRecipe>(_registered, StringComparer.OrdinalIgnoreCase);
                next.Remove(name);
                Save(next);
                _registered = next;
                return (true, null);
            }
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

        // H2: atomic write — a temp file plus File.Move(overwrite: true), which
        // is an atomic rename on NTFS. The previous File.WriteAllText directly
        // over the destination could be interrupted (process kill, power loss)
        // mid-write, leaving truncated JSON that LoadRegistered would then have
        // to quarantine on the next start — atomicity here is what keeps that
        // quarantine path rare instead of routine. Callers hold _writeLock.
        private void Save(Dictionary<string, OperationRecipe> registered)
        {
            try
            {
                var file = new OperationFile { SchemaVersion = CurrentSchemaVersion, Operations = registered.Values.ToList() };
                var temp = _registeredPath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(file, JsonOptions));
                File.Move(temp, _registeredPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to save registered operations: {ex.Message}");
            }
        }
    }
}
