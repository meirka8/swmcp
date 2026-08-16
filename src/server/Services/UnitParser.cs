using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace swmcp.server.Services
{
    /// <summary>
    /// Parses the unit sugar ADR 0001 assigns to the tool boundary. A
    /// <c>length</c> or <c>angle</c> param must always carry an explicit unit —
    /// either a quantity string (<c>"6 mm"</c>, <c>"0.25 in"</c>, <c>"30 deg"</c>)
    /// or an explicit SI quantity string (<c>"0.006 m"</c>, <c>"0.5 rad"</c>).
    /// This parsing is deliberately a swmcp/AI-client affordance — SwBridge
    /// stays SI-only.
    /// </summary>
    /// <remarks>
    /// UAT B1: a bare JSON number used to be accepted and treated as the SI
    /// canonical unit (meters/radians) — silently. <c>{"depth1": 6}</c> and
    /// <c>{"depth1": "6 mm"}</c> looked like the same request but differed by
    /// 1000x, and nothing in the response said which was applied. Requiring an
    /// explicit unit on every length/angle value (never a bare number, and
    /// never a numeric string with no unit suffix) closes that error class at
    /// the parser instead of relying on a plausibility check. Unitless params
    /// (counts, enum ints, bools) are unaffected — this parser is only ever
    /// consulted for <c>length</c>/<c>angle</c> param types.
    /// </remarks>
    public static class UnitParser
    {
        private static readonly Dictionary<string, double> LengthUnitsToMeters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["m"] = 1.0, ["meter"] = 1.0, ["meters"] = 1.0,
            ["mm"] = 0.001, ["millimeter"] = 0.001, ["millimeters"] = 0.001,
            ["cm"] = 0.01, ["centimeter"] = 0.01, ["centimeters"] = 0.01,
            ["in"] = 0.0254, ["inch"] = 0.0254, ["inches"] = 0.0254,
            ["ft"] = 0.3048, ["foot"] = 0.3048, ["feet"] = 0.3048,
        };

        private static readonly Dictionary<string, double> AngleUnitsToRadians = new(StringComparer.OrdinalIgnoreCase)
        {
            ["rad"] = 1.0, ["radian"] = 1.0, ["radians"] = 1.0,
            ["deg"] = Math.PI / 180.0, ["degree"] = Math.PI / 180.0, ["degrees"] = Math.PI / 180.0,
        };

        private const string LengthExamples = "'6 mm', '0.25 in', or an explicit SI quantity string like '0.006 m'";
        private const string AngleExamples = "'30 deg', or an explicit SI quantity string like '0.5 rad'";

        private static readonly Regex QuantityPattern = new(
            @"^\s*(?<number>-?[0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)\s*(?<unit>[a-zA-Z]*)\s*$",
            RegexOptions.Compiled);

        /// <summary>Parses a <c>length</c> param value to meters. Rejects a bare number — a unit is always required.</summary>
        public static bool TryParseLength(JsonElement value, out double meters, out string? error) =>
            TryParseQuantity(value, LengthUnitsToMeters, "length", LengthExamples, out meters, out error);

        /// <summary>Parses an <c>angle</c> param value to radians. Rejects a bare number — a unit is always required.</summary>
        public static bool TryParseAngle(JsonElement value, out double radians, out string? error) =>
            TryParseQuantity(value, AngleUnitsToRadians, "angle", AngleExamples, out radians, out error);

        private static bool TryParseQuantity(
            JsonElement value,
            IReadOnlyDictionary<string, double> unitTable,
            string kind,
            string examples,
            out double canonical,
            out string? error)
        {
            canonical = 0;
            error = null;

            if (value.ValueKind == JsonValueKind.Number)
            {
                error = $"A {kind} parameter must include a unit — got a bare number ({value.GetRawText()}). Use a quantity string, e.g. {examples}.";
                return false;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString() ?? "";
                var match = QuantityPattern.Match(text);
                if (!match.Success)
                {
                    error = $"Could not parse '{text}' as a {kind}. Use a quantity string, e.g. {examples}.";
                    return false;
                }

                var number = double.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
                var unit = match.Groups["unit"].Value;

                if (unit.Length == 0)
                {
                    error = $"A {kind} parameter must include a unit — got the bare number '{text}'. Use a quantity string, e.g. {examples}.";
                    return false;
                }

                if (!unitTable.TryGetValue(unit, out var factor))
                {
                    var known = string.Join(", ", unitTable.Keys);
                    error = $"Unknown {kind} unit '{unit}' in '{text}'. Known units: {known}.";
                    return false;
                }

                canonical = number * factor;
                return true;
            }

            error = $"A {kind} parameter needs a quantity string (e.g. {examples}), got JSON {value.ValueKind}.";
            return false;
        }
    }
}
