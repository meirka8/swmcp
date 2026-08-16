using SwBridge;

namespace swmcp.server.Services
{
    /// <summary>
    /// Filters the permanent tree-plumbing entries every SolidWorks document
    /// carries (folders, lights) out of a feature list by default (UAT
    /// re-verdict gap #4: "25 feature-tree entries for a 3-feature part is
    /// still 19 entries of noise burning client context").
    /// </summary>
    /// <remarks>
    /// The exact type-name list below is <b>observed data</b>, not a suffix
    /// heuristic (e.g. not "ends with Folder") — derived from a live
    /// <c>get_part_info</c> against <c>models/Part2.SLDPRT</c> and a fresh
    /// <c>new_part</c> scratch document, both of which report exactly 25
    /// feature-tree entries, 19 of which are these 16 type names (some appear
    /// more than once: two <c>NotesAreaFtrFolder</c>, three <c>DirectionLight</c>).
    /// Kept as data, not a naming-convention guess, because SolidWorks does not
    /// consistently name these types (<c>DetailCabinet</c>, <c>AmbientLight</c>
    /// and <c>DirectionLight</c> do not end in "Folder" despite being exactly
    /// the same kind of permanent, geometry-independent tree scaffolding as the
    /// ones that do).
    /// </remarks>
    public static class FeatureTreeFilter
    {
        public static readonly IReadOnlyCollection<string> FolderNoiseTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "CommentsFolder",
            "FavoriteFolder",
            "HistoryFolder",
            "SelectionSetFolder",
            "SensorFolder",
            "DocsFolder",
            "DetailCabinet",
            "SurfaceBodyFolder",
            "SolidBodyFolder",
            "EnvFolder",
            "InkMarkupFolder",
            "EqnFolder",
            "MaterialFolder",
            "NotesAreaFtrFolder",
            "AmbientLight",
            "DirectionLight",
        };

        /// <summary>True when <paramref name="typeName"/> is permanent tree plumbing rather than caller-created geometry.</summary>
        public static bool IsFolderNoise(string typeName) => FolderNoiseTypeNames.Contains(typeName);

        /// <summary>
        /// Returns <paramref name="features"/> unchanged when
        /// <paramref name="includeFolderFeatures"/> is true; otherwise filters
        /// out every entry whose <see cref="FeatureInfo.TypeName"/> is in
        /// <see cref="FolderNoiseTypeNames"/>. Order is preserved.
        /// </summary>
        public static IReadOnlyList<FeatureInfo> Apply(IReadOnlyList<FeatureInfo> features, bool includeFolderFeatures) =>
            includeFolderFeatures ? features : features.Where(f => !IsFolderNoise(f.TypeName)).ToList();
    }
}
