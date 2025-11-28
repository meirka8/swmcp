using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace swmcp.server.Services
{
    public class SchemaManager
    {
        private readonly string _filePath;
        private Dictionary<string, List<string>> _schemas;

        public SchemaManager()
        {
            // Use LocalApplicationData for persistence
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "swmcp");
            Directory.CreateDirectory(appDataPath);
            _filePath = Path.Combine(appDataPath, "known_features.json");

            // Check if we need to seed from the app directory
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

            _schemas = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            LoadSchemas();
        }

        private void LoadSchemas()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (loaded != null)
                    {
                        _schemas = loaded;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to load schemas: {ex.Message}");
                }
            }
        }

        private void SaveSchemas()
        {
            try
            {
                var json = JsonSerializer.Serialize(_schemas, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to save schemas: {ex.Message}");
            }
        }

        public List<string>? GetSchema(string featureType)
        {
            if (_schemas.TryGetValue(featureType, out var properties))
            {
                return properties;
            }
            return null;
        }

        public void RegisterSchema(string featureType, List<string> properties)
        {
            _schemas[featureType] = properties;
            SaveSchemas();
        }
    }
}
