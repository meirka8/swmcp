namespace SeedVerifier;

/// <summary>
/// Corrected spec candidates derived from Phase 1 (the actual interop member
/// lists). Live mode reads BOTH these and the researched specs off each created
/// feature so the report can say which of the two actually returns a value.
/// </summary>
public static class Candidates
{
    public static readonly Dictionary<string, List<SeedSpec>> Corrected = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Extrusion"] = new()
        {
            new("Depth", "GetDepth", new object?[] { true }),
            new("DraftAngle", "GetDraftAngle", new object?[] { true }),
            new("DraftOutward", "GetDraftOutward", new object?[] { true }),
            new("BothDirections", "BothDirections", null),
            new("ReverseDirection", "ReverseDirection", null),
            new("EndCondition", "GetEndCondition", new object?[] { true }),
            new("Merge", "Merge", null),
        },
        ["Cut"] = new()
        {
            new("Depth", "GetDepth", new object?[] { true }),
            new("DraftAngle", "GetDraftAngle", new object?[] { true }),
            new("DraftOutward", "GetDraftOutward", new object?[] { true }),
            new("BothDirections", "BothDirections", null),
            new("ReverseDirection", "ReverseDirection", null),
            new("FlipSideToCut", "FlipSideToCut", null),
        },
        ["Fillet"] = new()
        {
            new("DefaultRadius", "DefaultRadius", null),
            new("OverflowType", "OverflowType", null),
            new("FilletItemsCount", "FilletItemsCount", null),
            new("IsMultipleRadius", "IsMultipleRadius", null),
            new("Type", "Type", null),
            new("PropagateToTangentFaces", "PropagateToTangentFaces", null),
            new("EdgeCount", "GetEdgeCount", null),
            new("AsymmetricFillet", "AsymmetricFillet", null),
        },
        ["Chamfer"] = new()
        {
            new("Distance", "GetEdgeChamferDistance", new object?[] { 0 }),
            new("Distance2", "GetEdgeChamferDistance", new object?[] { 1 }),
            new("Angle", "EdgeChamferAngle", null),
            new("Type", "Type", null),
            new("EqualDistance", "EqualDistance", null),
            new("TangentPropagation", "TangentPropagation", null),
            new("EdgeCount", "GetEdgeCount", null),
        },
        ["CirPattern"] = new()
        {
            new("TotalInstances", "TotalInstances", null),
            new("Spacing", "Spacing", null),
            new("EqualSpacing", "EqualSpacing", null),
            new("Symmetric", "Symmetric", null),
            new("ReverseDirection", "ReverseDirection", null),
            new("GeometryPattern", "GeometryPattern", null),
            // researched, expected to fail:
            new("D1TotalAngle", "D1TotalAngle", null),
        },
        ["LPattern"] = new()
        {
            new("D1TotalInstances", "D1TotalInstances", null),
            new("D1Spacing", "D1Spacing", null),
            new("D2TotalInstances", "D2TotalInstances", null),
            new("D2Spacing", "D2Spacing", null),
            new("D1ReverseDirection", "D1ReverseDirection", null),
            new("GeometryPattern", "GeometryPattern", null),
            // researched, expected to fail:
            new("D1Instances", "D1Instances", null),
            new("D2Instances", "D2Instances", null),
        },
        ["MirrorPattern"] = new()
        {
            new("Plane", "Plane", null),
            new("MirrorPlaneType", "GetMirrorPlaneType", null),
            new("PatternFeatureCount", "GetPatternFeatureCount", null),
            new("GeometryPattern", "GeometryPattern", null),
            // researched, expected to fail:
            new("MirrorPlane", "MirrorPlane", null),
        },
        ["Revolution"] = new()
        {
            new("Angle", "GetRevolutionAngle", new object?[] { true }),
            new("ReverseDirection", "ReverseDirection", null),
            new("Type", "Type", null),
            new("Merge", "Merge", null),
            // researched, expected to fail:
            new("GetAngle", "GetAngle", new object?[] { true }),
        },
        ["RevCut"] = new()
        {
            new("Angle", "GetRevolutionAngle", new object?[] { true }),
            new("ReverseDirection", "ReverseDirection", null),
            new("Type", "Type", null),
            new("GetAngle", "GetAngle", new object?[] { true }),
        },
        ["Shell"] = new()
        {
            new("Thickness", "Thickness", null),
            new("Direction", "Direction", null),
            new("FacesRemovedCount", "FacesRemovedCount", null),
        },
        ["Draft"] = new()
        {
            new("Angle", "Angle", null),
            new("Type", "Type", null),
            new("ReverseDirection", "ReverseDirection", null),
            new("FacePropagation", "FacePropagation", null),
            // researched, expected to fail:
            new("DraftAngle", "DraftAngle", null),
        },
        ["Dome"] = new()
        {
            new("Height", "Height", null),
            new("Elliptical", "Elliptical", null),
            new("ReverseDir", "ReverseDir", null),
            // researched, expected to fail:
            new("BlendOption", "BlendOption", null),
        },
        ["RefPlane"] = new()
        {
            new("Distance", "Distance", null),
            new("Angle", "Angle", null),
            new("Type", "Type", null),
            new("Reference", "Reference", new object?[] { 0 }),
            new("Constraint", "Constraint", new object?[] { 0 }),
            new("SelectionsCount", "GetSelectionsCount", null),
            // researched, expected to fail (indexed property read with no arg):
            new("ReferenceBare", "Reference", null),
        },
        ["Rib"] = new()
        {
            new("Thickness", "Thickness", null),
            new("DraftAngle", "DraftAngle", null),
            new("IsTwoSided", "IsTwoSided", null),
            new("EnableDraft", "EnableDraft", null),
        },
        ["HoleWzd"] = new()
        {
            new("Type", "Type", null),
            new("MajorDiameter", "MajorDiameter", null),
            new("Diameter", "Diameter", null),
            new("Depth", "Depth", null),
            new("ReverseDirection", "ReverseDirection", null),
            new("Standard", "Standard", null),
            new("FastenerType", "FastenerType", null),
            new("HoleType", "HoleType", null),
        },
        ["SweepThread"] = new()
        {
            new("ThreadStartAngle", "ThreadStartAngle", null),
            new("Pitch", "Pitch", null),
            new("Diameter", "Diameter", null),
            new("MaintainThreadLength", "MaintainThreadLength", null),
            new("RightHanded", "RightHanded", null),
            new("Revolutions", "Revolutions", null),
        },
    };
}
