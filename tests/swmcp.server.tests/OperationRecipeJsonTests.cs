using System.Text.Json;
using SwBridge;
using swmcp.server.Models;
using swmcp.server.Services;
using Xunit;

namespace swmcp.server.tests
{
    /// <summary>
    /// Round-trips the shipped known_operations.json seed and validates every
    /// recipe's shape. Constructs real SwBridge types (SwConnection,
    /// DocumentManager) but never calls anything that touches a live
    /// SolidWorks instance — SwConnection attaches lazily, so this runs
    /// without SolidWorks installed.
    /// </summary>
    public class OperationRecipeJsonTests
    {
        private static readonly string SeedPath = Path.Combine(AppContext.BaseDirectory, "known_operations.json");

        private static readonly string[] ExpectedWasherOperations =
        {
            "new_part", "select_by_id", "clear_selection", "insert_sketch", "exit_sketch",
            "create_circle_by_radius", "create_line", "extrude_boss", "rebuild", "undo",
        };

        [Fact]
        public void SeedFile_Exists_NextToTestOutput()
        {
            Assert.True(File.Exists(SeedPath), $"Expected the seed to be copied to '{SeedPath}'.");
        }

        [Fact]
        public void SeedFile_ParsesAsSchemaVersion1_WithExpectedOperations()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var file = JsonSerializer.Deserialize<OperationFile>(File.ReadAllText(SeedPath), options);

            Assert.NotNull(file);
            Assert.Equal(1, file!.SchemaVersion);

            var names = file.Operations.Select(o => o.Name).ToList();
            foreach (var expected in ExpectedWasherOperations)
            {
                Assert.Contains(expected, names);
            }
        }

        [Fact]
        public void SeedFile_RoundTrips_ThroughSerializeDeserialize()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
            var original = JsonSerializer.Deserialize<OperationFile>(File.ReadAllText(SeedPath), options)!;

            var roundTripped = JsonSerializer.Deserialize<OperationFile>(JsonSerializer.Serialize(original, options), options)!;

            Assert.Equal(original.Operations.Count, roundTripped.Operations.Count);
            Assert.Equal(
                original.Operations.Select(o => o.Name).OrderBy(n => n),
                roundTripped.Operations.Select(o => o.Name).OrderBy(n => n));

            // extrude_boss's 23-param FeatureExtrusion3 signature is the highest-risk
            // spot for a round-trip to silently drop or reorder a param.
            var beforeExtrude = original.Operations.Single(o => o.Name == "extrude_boss");
            var afterExtrude = roundTripped.Operations.Single(o => o.Name == "extrude_boss");
            Assert.Equal(beforeExtrude.Params.Count, afterExtrude.Params.Count);
            Assert.Equal(beforeExtrude.Params.Select(p => p.Name), afterExtrude.Params.Select(p => p.Name));
        }

        [Fact]
        public void EverySeedRecipe_PassesShapeValidation()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            foreach (var recipe in manager.List())
            {
                var (ok, error, _) = manager.Validate(recipe);
                Assert.True(ok, $"'{recipe.Name}' failed validation: {error}");
            }
        }

        [Fact]
        public void OperationManager_List_ContainsEveryWasherOperation()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var names = manager.List().Select(o => o.Name).ToList();
            foreach (var expected in ExpectedWasherOperations)
            {
                Assert.Contains(expected, names);
                Assert.Equal("seed", manager.Get(expected)!.Source);
            }
        }

        [Fact]
        public void ExtrudeBoss_Has23PositionalParams_MatchingFeatureExtrusion3()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var extrude = manager.Get("extrude_boss");
            Assert.NotNull(extrude);
            Assert.Equal(23, extrude!.Params.Count);
            Assert.Equal("FeatureManager", extrude.Target);
            Assert.Equal("FeatureExtrusion3", extrude.Member);
        }
    }
}
