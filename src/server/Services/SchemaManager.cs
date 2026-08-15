using System.Text.Json;
using SwBridge;

namespace swmcp.server.Services
{
    /// <summary>
    /// The dynamic feature-schema registry: featureTypeName → property specs.
    /// Ships with a seed (known_features.json) but is never limited to it —
    /// new schemas are registered at runtime and persisted to LOCALAPPDATA.
    /// </summary>
    public class SchemaManager
    {
        private readonly string _filePath;
        private Dictionary<string, List<PropertySpec>> _schemas =
            new(StringComparer.OrdinalIgnoreCase);

        public SchemaManager()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "swmcp");
            Directory.CreateDirectory(appDataPath);
            _filePath = Path.Combine(appDataPath, "known_features.json");

            if (!File.Exists(_filePath))
            {
                var seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "known_features.json");
                if (File.Exists(seedPath))
                {
                    try
                    {
                        File.Copy(seedPath, _filePath);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to copy seed file: {ex.Message}");
                    }
                }
            }

            LoadSchemas();
        }

        public IReadOnlyList<PropertySpec>? GetSchema(string featureType) =>
            _schemas.TryGetValue(featureType, out var specs) ? specs : null;

        public IReadOnlyCollection<string> KnownFeatureTypes => _schemas.Keys;

        public void RegisterSchema(string featureType, List<PropertySpec> specs)
        {
            _schemas[featureType] = specs;
            SaveSchemas();
        }

        private void LoadSchemas()
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_filePath));
                var loaded = new Dictionary<string, List<PropertySpec>>(StringComparer.OrdinalIgnoreCase);
                foreach (var featureType in doc.RootElement.EnumerateObject())
                {
                    var specs = new List<PropertySpec>();
                    foreach (var entry in featureType.Value.EnumerateArray())
                    {
                        var spec = ParseSpec(entry);
                        if (spec != null)
                        {
                            specs.Add(spec);
                        }
                    }
                    loaded[featureType.Name] = specs;
                }
                _schemas = loaded;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load schemas: {ex.Message}");
            }
        }

        // Accepts the current object form {name, member, args} and the legacy
        // form of a bare string (treated as an argument-less property).
        private static PropertySpec? ParseSpec(JsonElement entry)
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var name = entry.GetString();
                return string.IsNullOrWhiteSpace(name) ? null : PropertySpec.Bare(name);
            }

            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("name", out var nameElement) ||
                nameElement.GetString() is not { Length: > 0 } specName)
            {
                return null;
            }

            var member = entry.TryGetProperty("member", out var memberElement)
                ? memberElement.GetString() ?? specName
                : specName;

            List<object?>? args = null;
            if (entry.TryGetProperty("args", out var argsElement) &&
                argsElement.ValueKind == JsonValueKind.Array)
            {
                args = new List<object?>();
                foreach (var arg in argsElement.EnumerateArray())
                {
                    args.Add(ToClrValue(arg));
                }
            }

            return new PropertySpec(specName, member, args);
        }

        /// <summary>
        /// JSON values must become CLR primitives before they can cross into COM
        /// dispatch — a JsonElement marshals as garbage.
        /// </summary>
        public static object? ToClrValue(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            _ => null,
        };

        private void SaveSchemas()
        {
            try
            {
                var serializable = new Dictionary<string, List<object>>();
                foreach (var (featureType, specs) in _schemas)
                {
                    var entries = new List<object>();
                    foreach (var spec in specs)
                    {
                        entries.Add(new { name = spec.Name, member = spec.Member, args = spec.Args });
                    }
                    serializable[featureType] = entries;
                }

                var json = JsonSerializer.Serialize(serializable, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to save schemas: {ex.Message}");
            }
        }
    }
}
