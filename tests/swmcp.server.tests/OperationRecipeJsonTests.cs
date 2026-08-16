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

        private static readonly string[] ExpectedPromotedOperations =
        {
            "cut_extrude", "fillet_constant_radius", "select_by_ray", "set_material", "save_as", "create_corner_rectangle",
        };

        private static OperationManager NewManagerInTempDir(out string tempDir, string? seedJson = null)
        {
            tempDir = Directory.CreateTempSubdirectory("swmcp-op-tests-").FullName;
            var seedPath = Path.Combine(tempDir, "seed.json");
            File.WriteAllText(seedPath, seedJson ?? "{\"schemaVersion\":1,\"operations\":[]}");
            var registeredPath = Path.Combine(tempDir, "registered.json");

            var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            return new OperationManager(connection, documents, seedPath, registeredPath);
        }

        private static OperationRecipe MinimalValidRecipe(string name) => new()
        {
            Name = name,
            Summary = "test recipe",
            Scope = "document",
            Target = "",
            Kind = "method",
            Member = "ClearSelection2",
            Params = new List<OperationParam> { new() { Name = "all", Type = "bool", Default = JsonDocument.Parse("true").RootElement.Clone() } },
            Returns = new ReturnsSpec { Type = "void" },
            Verify = new List<VerifyCheck>(),
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
            foreach (var expected in ExpectedWasherOperations.Concat(ExpectedPromotedOperations))
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

            // extrude_boss's 23-param FeatureExtrusion3 signature and
            // cut_extrude's 27-param FeatureCut4 signature are the highest-risk
            // spots for a round-trip to silently drop or reorder a param.
            foreach (var name in new[] { "extrude_boss", "cut_extrude", "fillet_constant_radius" })
            {
                var before = original.Operations.Single(o => o.Name == name);
                var after = roundTripped.Operations.Single(o => o.Name == name);
                Assert.Equal(before.Params.Count, after.Params.Count);
                Assert.Equal(before.Params.Select(p => p.Name), after.Params.Select(p => p.Name));
            }
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
        public void OperationManager_List_ContainsEveryPromotedOperation()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var names = manager.List().Select(o => o.Name).ToList();
            foreach (var expected in ExpectedPromotedOperations)
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

        [Fact]
        public void CutExtrude_Has27PositionalParams_MatchingFeatureCut4()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var cut = manager.Get("cut_extrude");
            Assert.NotNull(cut);
            Assert.Equal(27, cut!.Params.Count);
            Assert.Equal("FeatureManager", cut.Target);
            Assert.Equal("FeatureCut4", cut.Member);
        }

        [Fact]
        public void FilletConstantRadius_Has14PositionalParams_MatchingFeatureFillet3()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var fillet = manager.Get("fillet_constant_radius");
            Assert.NotNull(fillet);
            Assert.Equal(14, fillet!.Params.Count);
            Assert.Equal("FeatureManager", fillet.Target);
            Assert.Equal("FeatureFillet3", fillet.Member);
        }

        [Fact]
        public void SaveAs_UsesReturnEquals_NotReturnTrue()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var saveAs = manager.Get("save_as");
            Assert.NotNull(saveAs);
            var check = Assert.Single(saveAs!.Verify);
            Assert.Equal("returnEquals", check.Check, ignoreCase: true);
            Assert.NotNull(check.Expected);
        }

        // M4: application-scoped recipes cannot declare 'requires' — rejected
        // at validation time so a bad recipe never gets persisted.
        [Fact]
        public void Validate_RejectsRequiresOnApplicationScopedRecipe()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var recipe = new OperationRecipe
            {
                Name = "bad_app_scoped",
                Scope = "application",
                Member = "SomeMethod",
                Requires = new List<RequireCheck> { new() { Check = "notInSketchMode" } },
            };

            var (ok, error, _) = manager.Validate(recipe);

            Assert.False(ok);
            Assert.Contains("Application-scoped", error);
        }

        [Fact]
        public void Validate_ReturnEqualsWithoutExpected_Fails()
        {
            using var connection = new SwConnection();
            var documents = new DocumentManager(connection);
            var manager = new OperationManager(connection, documents);

            var recipe = new OperationRecipe
            {
                Name = "bad_return_equals",
                Scope = "document",
                Member = "SomeMethod",
                Verify = new List<VerifyCheck> { new() { Check = "returnEquals" } },
            };

            var (ok, error, _) = manager.Validate(recipe);

            Assert.False(ok);
            Assert.Contains("expected", error, StringComparison.OrdinalIgnoreCase);
        }

        // H2: a malformed registered-store file must be quarantined (renamed
        // with a timestamp), never silently treated as empty and then
        // overwritten on the next Save.
        [Fact]
        public void MalformedRegisteredStore_IsQuarantined_NotSilentlyOverwritten()
        {
            var tempDir = Directory.CreateTempSubdirectory("swmcp-op-tests-").FullName;
            try
            {
                var seedPath = Path.Combine(tempDir, "seed.json");
                File.WriteAllText(seedPath, "{\"schemaVersion\":1,\"operations\":[]}");
                var registeredPath = Path.Combine(tempDir, "registered.json");
                File.WriteAllText(registeredPath, "{ this is not valid json at all");

                using var connection = new SwConnection();
                var documents = new DocumentManager(connection);
                var manager = new OperationManager(connection, documents, seedPath, registeredPath);

                Assert.False(File.Exists(registeredPath));
                var quarantined = Directory.GetFiles(tempDir, "registered.json.bad-*");
                Assert.Single(quarantined);
                Assert.Contains("this is not valid json", File.ReadAllText(quarantined[0]));

                // Registering afterward must succeed and write atomically (no
                // leftover .tmp file), proving the corrupted-store recovery
                // path does not also break ordinary persistence.
                var (ok, error, warnings) = manager.Register(MinimalValidRecipe("temp_test_op"));
                Assert.True(ok, error);
                Assert.Contains(warnings, w => w.Contains("quarantined", StringComparison.OrdinalIgnoreCase));
                Assert.True(File.Exists(registeredPath));
                Assert.False(File.Exists(registeredPath + ".tmp"));
                Assert.Equal("temp_test_op", manager.Get("temp_test_op")?.Name);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void MissingRegisteredStore_IsTreatedAsEmpty_NotAnError()
        {
            var manager = NewManagerInTempDir(out var tempDir);
            try
            {
                Assert.Empty(manager.List());
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void Register_ThenPersistsAcrossReload_ViaAtomicWrite()
        {
            var tempDir = Directory.CreateTempSubdirectory("swmcp-op-tests-").FullName;
            try
            {
                var seedPath = Path.Combine(tempDir, "seed.json");
                File.WriteAllText(seedPath, "{\"schemaVersion\":1,\"operations\":[]}");
                var registeredPath = Path.Combine(tempDir, "registered.json");

                using var connection = new SwConnection();
                var documents = new DocumentManager(connection);

                var first = new OperationManager(connection, documents, seedPath, registeredPath);
                var (ok, error, _) = first.Register(MinimalValidRecipe("persisted_op"));
                Assert.True(ok, error);

                var second = new OperationManager(connection, documents, seedPath, registeredPath);
                Assert.Equal("persisted_op", second.Get("persisted_op")?.Name);
                Assert.Equal("registered", second.Get("persisted_op")?.Source);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        // unregister_operation semantics: removes a registered recipe;
        // refuses (rather than silently no-op-ing) for a seed name or an
        // unknown name.
        [Fact]
        public void Unregister_RemovesRegisteredRecipe()
        {
            var manager = NewManagerInTempDir(out var tempDir);
            try
            {
                var (registerOk, registerError, _) = manager.Register(MinimalValidRecipe("removable_op"));
                Assert.True(registerOk, registerError);
                Assert.NotNull(manager.Get("removable_op"));

                var (ok, error) = manager.Unregister("removable_op");

                Assert.True(ok, error);
                Assert.Null(manager.Get("removable_op"));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void Unregister_RefusesSeedOperation()
        {
            var manager = NewManagerInTempDir(
                out var tempDir,
                seedJson: JsonSerializer.Serialize(new OperationFile
                {
                    SchemaVersion = 1,
                    Operations = new List<OperationRecipe> { MinimalValidRecipe("a_seed_op") },
                }));
            try
            {
                var (ok, error) = manager.Unregister("a_seed_op");

                Assert.False(ok);
                Assert.Contains("seed operation", error, StringComparison.OrdinalIgnoreCase);
                Assert.NotNull(manager.Get("a_seed_op")); // untouched
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void Unregister_RefusesUnknownName()
        {
            var manager = NewManagerInTempDir(out var tempDir);
            try
            {
                var (ok, error) = manager.Unregister("does_not_exist_anywhere");

                Assert.False(ok);
                Assert.NotNull(error);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void Unregister_OfShadowingRecipe_RestoresSeedVersion()
        {
            var manager = NewManagerInTempDir(
                out var tempDir,
                seedJson: JsonSerializer.Serialize(new OperationFile
                {
                    SchemaVersion = 1,
                    Operations = new List<OperationRecipe> { MinimalValidRecipe("shadowed_op") },
                }));
            try
            {
                var overriding = MinimalValidRecipe("shadowed_op");
                overriding.Summary = "an overriding version";
                var (registerOk, registerError, _) = manager.Register(overriding);
                Assert.True(registerOk, registerError);
                Assert.Equal("registered", manager.Get("shadowed_op")?.Source);

                var (ok, error) = manager.Unregister("shadowed_op");

                Assert.True(ok, error);
                Assert.Equal("seed", manager.Get("shadowed_op")?.Source);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
