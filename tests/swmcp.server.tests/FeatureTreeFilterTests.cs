using SwBridge;
using swmcp.server.Services;
using Xunit;

namespace swmcp.server.tests
{
    /// <summary>
    /// Pure logic — no SolidWorks required. The exact type-name list is
    /// derived from a live get_part_info against Part2.SLDPRT / a fresh
    /// new_part (see FeatureTreeFilter's own remarks); these tests fix that
    /// contract so a future edit cannot silently drop or add a noise type
    /// without a test noticing.
    /// </summary>
    public class FeatureTreeFilterTests
    {
        private static FeatureInfo Feature(string name, string typeName) => new(name, typeName, null);

        [Fact]
        public void Apply_DefaultExcludesFolderNoise()
        {
            var features = new[]
            {
                Feature("Comments", "CommentsFolder"),
                Feature("Front Plane", "RefPlane"),
                Feature("Boss-Extrude1", "Extrusion"),
            };

            var result = FeatureTreeFilter.Apply(features, includeFolderFeatures: false);

            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result, f => f.TypeName == "CommentsFolder");
            Assert.Contains(result, f => f.Name == "Front Plane");
            Assert.Contains(result, f => f.Name == "Boss-Extrude1");
        }

        [Fact]
        public void Apply_IncludeFolderFeaturesTrue_ReturnsEverythingUnchanged()
        {
            var features = new[]
            {
                Feature("Comments", "CommentsFolder"),
                Feature("Boss-Extrude1", "Extrusion"),
            };

            var result = FeatureTreeFilter.Apply(features, includeFolderFeatures: true);

            Assert.Equal(features.Length, result.Count);
            Assert.Same(features[0], result[0]);
            Assert.Same(features[1], result[1]);
        }

        [Fact]
        public void Apply_PreservesOrder()
        {
            var features = new[]
            {
                Feature("Front Plane", "RefPlane"),
                Feature("Notes", "NotesAreaFtrFolder"),
                Feature("Sketch1", "ProfileFeature"),
                Feature("Boss-Extrude1", "Extrusion"),
            };

            var result = FeatureTreeFilter.Apply(features, includeFolderFeatures: false);

            Assert.Equal(new[] { "Front Plane", "Sketch1", "Boss-Extrude1" }, result.Select(f => f.Name));
        }

        // The exact 25-entries-observed / 19-noise-entries ratio the UAT
        // re-verdict cites (gap #4) — fixes the full observed set so the
        // "folder-free by default" claim in DOCUMENTATION.md stays true.
        [Fact]
        public void Apply_MatchesLiveObservedBaseline_19NoiseOf25()
        {
            var features = new[]
            {
                Feature("Comments", "CommentsFolder"),
                Feature("Favorites", "FavoriteFolder"),
                Feature("History", "HistoryFolder"),
                Feature("Selection Sets", "SelectionSetFolder"),
                Feature("Sensors", "SensorFolder"),
                Feature("Design Binder", "DocsFolder"),
                Feature("Annotations", "DetailCabinet"),
                Feature("Surface Bodies", "SurfaceBodyFolder"),
                Feature("Solid Bodies", "SolidBodyFolder"),
                Feature("Lights and Cameras", "EnvFolder"),
                Feature("Markups", "InkMarkupFolder"),
                Feature("Equations", "EqnFolder"),
                Feature("Material <not specified>", "MaterialFolder"),
                Feature("Front Plane", "RefPlane"),
                Feature("Top Plane", "RefPlane"),
                Feature("Right Plane", "RefPlane"),
                Feature("Origin", "OriginProfileFeature"),
                Feature("Sketch1", "ProfileFeature"),
                Feature("Boss-Extrude1", "Extrusion"),
                Feature("Notes", "NotesAreaFtrFolder"),
                Feature("Notes1___EndTag___", "NotesAreaFtrFolder"),
                Feature("Ambient", "AmbientLight"),
                Feature("Directional1", "DirectionLight"),
                Feature("Directional2", "DirectionLight"),
                Feature("Directional3", "DirectionLight"),
            };

            Assert.Equal(25, features.Length);

            var result = FeatureTreeFilter.Apply(features, includeFolderFeatures: false);

            Assert.Equal(6, result.Count); // 3 planes + Origin + Sketch1 + Boss-Extrude1
            Assert.Equal(
                new[] { "Front Plane", "Top Plane", "Right Plane", "Origin", "Sketch1", "Boss-Extrude1" },
                result.Select(f => f.Name));
        }

        [Fact]
        public void IsFolderNoise_KnownTypesTrue_RealGeometryTypesFalse()
        {
            Assert.True(FeatureTreeFilter.IsFolderNoise("CommentsFolder"));
            Assert.True(FeatureTreeFilter.IsFolderNoise("DetailCabinet"));
            Assert.True(FeatureTreeFilter.IsFolderNoise("AmbientLight"));
            Assert.True(FeatureTreeFilter.IsFolderNoise("DirectionLight"));

            Assert.False(FeatureTreeFilter.IsFolderNoise("Extrusion"));
            Assert.False(FeatureTreeFilter.IsFolderNoise("Fillet"));
            Assert.False(FeatureTreeFilter.IsFolderNoise("RefPlane"));
            Assert.False(FeatureTreeFilter.IsFolderNoise("ProfileFeature"));
            Assert.False(FeatureTreeFilter.IsFolderNoise("OriginProfileFeature"));
        }
    }
}
