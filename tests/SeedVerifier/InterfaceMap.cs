namespace SeedVerifier;

/// <summary>
/// Feature type name (IFeature.GetTypeName2()) -> the definition interface the
/// research report claims IFeature.GetDefinition() returns for it.
/// Phase 1 checks the researched specs against these interfaces statically;
/// Phase 2 QI-probes live definitions to confirm the mapping is real.
/// </summary>
public static class InterfaceMap
{
    public static readonly Dictionary<string, string> Researched = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Extrusion"] = "IExtrudeFeatureData2",
        ["Cut"] = "IExtrudeFeatureData2",
        ["ICE"] = "IExtrudeFeatureData2",
        ["Fillet"] = "ISimpleFilletFeatureData2",
        ["Chamfer"] = "IChamferFeatureData2",
        ["CirPattern"] = "ICircularPatternFeatureData",
        ["LPattern"] = "ILinearPatternFeatureData",
        ["MirrorPattern"] = "IMirrorPatternFeatureData",
        ["Revolution"] = "IRevolveFeatureData2",
        ["RevCut"] = "IRevolveFeatureData2",
        ["Sweep"] = "ISweepFeatureData",
        ["Loft"] = "ILoftFeatureData",
        ["HoleWzd"] = "IWizardHoleFeatureData2",
        ["RefPlane"] = "IRefPlaneFeatureData",
        ["Shell"] = "IShellFeatureData",
        ["Rib"] = "IRibFeatureData",
        ["Draft"] = "IDraftFeatureData2",
        ["Dome"] = "IDomeFeatureData",
        // Corrected from the research report's "ISweepThreadFeatureData",
        // which does not exist in SolidWorks.Interop.sldworks.
        ["SweepThread"] = "IThreadFeatureData",
        // Not in the research report; discovered live (IModelDoc2.InsertAxis2
        // produces a feature whose GetTypeName2() is "RefAxis").
        ["RefAxis"] = "IRefAxisFeatureData",
    };
}
