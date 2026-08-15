using System.Text.Json;
using System.Text.Json.Serialization;

namespace swmcp.server.Models
{
    /// <summary>
    /// One named parameter of an <see cref="OperationRecipe"/>. Params bind by
    /// name (never position) — see ADR 0001 §1, "the single highest-value
    /// decision in the ADR".
    /// </summary>
    public sealed class OperationParam
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// One of: <c>bool</c>, <c>int</c>, <c>double</c>, <c>string</c>,
        /// <c>length</c> (meters, or a quantity string like "5 mm"),
        /// <c>angle</c> (radians, or a quantity string like "30 deg"),
        /// <c>enum</c> (a plain int; see <see cref="EnumName"/> for which
        /// SolidWorks enum it documents), or <c>comNull</c> (a COM-interface
        /// parameter that must be passed as a null interface pointer —
        /// callers never supply a value for this; the runner always binds a
        /// <c>System.Runtime.InteropServices.DispatchWrapper(null)</c>, the
        /// live-verified fix for the DISP_E_TYPEMISMATCH a bare null produces).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        /// <summary>Documentation-only: which SolidWorks enum this int corresponds to, e.g. "swEndConditions_e".</summary>
        [JsonPropertyName("enum")]
        public string? EnumName { get; set; }

        /// <summary>
        /// Default value, or null when there is none. Nullable (rather than a
        /// bare <see cref="JsonElement"/> defaulting to <c>Undefined</c>) so
        /// round-tripping a param with no default never tries to serialize an
        /// unbacked <c>default(JsonElement)</c>, which throws.
        /// </summary>
        [JsonPropertyName("default")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Default { get; set; }

        [JsonPropertyName("required")]
        public bool Required { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonIgnore]
        public bool HasDefault => Default.HasValue && Default.Value.ValueKind != JsonValueKind.Undefined;
    }

    /// <summary>
    /// One precondition. Closed v1 vocabulary (ADR 0001 §1): <c>documentType</c>
    /// (Value = "part"/"assembly"/"drawing"), <c>inSketchMode</c>,
    /// <c>notInSketchMode</c>, <c>selectionCount</c> (Min/Max), <c>selectionType</c>
    /// (Mark required; Type optional — a <c>swSelectType_e</c> integer given as a
    /// string, since SwBridge 0.4.0 has no selection-type probe and this is
    /// evaluated generically via <c>SelectionMgr.GetSelectedObjectType3</c> — a
    /// documented deviation from the ADR's unspecified <c>type</c> shape).
    /// </summary>
    public sealed class RequireCheck
    {
        [JsonPropertyName("check")]
        public string Check { get; set; } = "";

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("min")]
        public int? Min { get; set; }

        [JsonPropertyName("max")]
        public int? Max { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("mark")]
        public int? Mark { get; set; }
    }

    /// <summary>
    /// One post-condition (ADR 0002). Closed v1 vocabulary: <c>returnNotNull</c>,
    /// <c>returnTrue</c>, <c>featureCountIncreased</c> (By, default 1),
    /// <c>sketchSegmentCountIncreased</c> (By, default 1), <c>sketchModeIs</c>
    /// (Value), <c>noNewRebuildErrors</c>.
    /// </summary>
    public sealed class VerifyCheck
    {
        [JsonPropertyName("check")]
        public string Check { get; set; } = "";

        [JsonPropertyName("by")]
        public int? By { get; set; }

        [JsonPropertyName("value")]
        public bool? Value { get; set; }
    }

    /// <summary>
    /// The DTO shape a recipe's invocation result converts to. v1 shapes:
    /// <c>void</c>, <c>bool</c>, <c>number</c>, <c>string</c>, <c>feature</c>,
    /// <c>sketchSegment</c>, <c>sketchSegments</c>, and <c>document</c> — the
    /// last is an addition beyond ADR 0001's listed shapes, needed for
    /// <c>new_part</c> to return the created document's identity (the ADR
    /// itself says new_part "returns the new document's title" but never
    /// enumerates a shape for it).
    /// </summary>
    public sealed class ReturnsSpec
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "void";
    }

    /// <summary>
    /// One operation: a single declared COM invocation, described as data —
    /// the write-side analogue of SwBridge's <c>PropertySpec</c> (ADR 0001).
    /// </summary>
    public sealed class OperationRecipe
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        /// <summary><c>"application"</c> (root = the ISldWorks app) or <c>"document"</c> (root = the resolved document).</summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = "document";

        /// <summary>
        /// Dotted, read-only path from the scope root to the object the
        /// terminal member is invoked on, e.g. <c>"FeatureManager"</c>,
        /// <c>"Extension.SelectionManager"</c>. Empty string means "the root
        /// itself" (<see cref="SwBridge.ComPath.Resolve"/>).
        /// </summary>
        [JsonPropertyName("target")]
        public string Target { get; set; } = "";

        /// <summary><c>"method"</c>, <c>"propertySet"</c>, or <c>"propertyGet"</c> — never combined (ADR 0001 §1).</summary>
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "method";

        /// <summary>The COM member invoked on the resolved target.</summary>
        [JsonPropertyName("member")]
        public string Member { get; set; } = "";

        [JsonPropertyName("requires")]
        public List<RequireCheck> Requires { get; set; } = new();

        [JsonPropertyName("params")]
        public List<OperationParam> Params { get; set; } = new();

        [JsonPropertyName("returns")]
        public ReturnsSpec Returns { get; set; } = new();

        [JsonPropertyName("verify")]
        public List<VerifyCheck> Verify { get; set; } = new();

        /// <summary><c>"seed"</c> or <c>"registered"</c> — set by <c>OperationManager</c> on load, ignored on input.</summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = "seed";

        [JsonPropertyName("verifiedOn")]
        public string? VerifiedOn { get; set; }
    }

    /// <summary>Root shape of both the shipped seed and the persisted registered-operations store.</summary>
    public sealed class OperationFile
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("operations")]
        public List<OperationRecipe> Operations { get; set; } = new();
    }
}
