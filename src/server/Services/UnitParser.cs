using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace swmcp.server.Services
{
    /// <summary>
    /// Parses the unit sugar ADR 0001 assigns to the tool boundary: a
    /// <c>length</c> param accepts a bare number (meters, the SI canonical
    /// unit SwBridge uses at the COM boundary) or a quantity string like
    /// <c>"5 mm"</c>; an <c>angle</c> param accepts radians or e.g. <c>"30 deg"</c>.
    /// This parsing is deliberately a swmcp/AI-client affordance — SwBridge
    /// stays SI-only.
    /// </summary>
    public static class UnitParser
    {
        private static readonly Dictionary<string, double> LengthUnitsToMeters = new(StringComparer.OrdinalIgnoreCase)
        {
            [""] = 1.0,
            ["m"] = 1.0, ["meter"] = 1.0, ["meters"] = 1.0,
            ["mm"] = 0.001, ["millimeter"] = 0.001, ["millimeters"] = 0.001,
            ["cm"] = 0.01, ["centimeter"] = 0.01, ["centimeters"] = 0.01,
            ["in"] = 0.0254, ["inch"] = 0.0254, ["inches"] = 0.0254,
            ["ft"] = 0.3048, ["foot"] = 0.3048, ["feet"] = 0.3048,
        };

        private static readonly Dictionary<string, double> AngleUnitsToRadians = new(StringComparer.OrdinalIgnoreCase)
        {
            [""] = 1.0,
            ["rad"] = 1.0, ["radian"] = 1.0, ["radians"] = 1.0,
            ["deg"] = Math.PI / 180.0, ["degree"] = Math.PI / 180.0, ["degrees"] = Math.PI / 180.0,
        };

        private static readonly Regex QuantityPattern = new(
            @"^\s*(?<number>-?[0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)\s*(?<unit>[a-zA-Z]*)\s*$",
            RegexOptions.Compiled);

        /// <summary>Parses a <c>length</c> param value to meters.</summary>
        public static bool TryParseLength(JsonElement value, out double meters, out string? error) =>
            TryParseQuantity(value, LengthUnitsToMeters, "length", "meters, or a quantity string like '5 mm'", out meters, out error);

        /// <summary>Parses an <c>angle</c> param value to radians.</summary>
        public static bool TryParseAngle(JsonElement value, out double radians, out string? error) =>
            TryParseQuantity(value, AngleUnitsToRadians, "angle", "radians, or a quantity string like '30 deg'", out radians, out error);

        private static bool TryParseQuantity(
            JsonElement value,
            IReadOnlyDictionary<string, double> unitTable,
            string kind,
            string expected,
            out double canonical,
            out string? error)
        {
            canonical = 0;
            error = null;

            if (value.ValueKind == JsonValueKind.Number)
            {
                canonical = value.GetDouble();
                return true;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString() ?? "";
                var match = QuantityPattern.Match(text);
                if (!match.Success)
                {
                    error = $"Could not parse '{text}' as a {kind} ({expected}).";
                    return false;
                }

                var number = double.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
                var unit = match.Groups["unit"].Value;
                if (!unitTable.TryGetValue(unit, out var factor))
                {
                    var known = string.Join(", ", unitTable.Keys.Where(k => k.Length > 0));
                    error = $"Unknown {kind} unit '{unit}' in '{text}'. Known units: {known}.";
                    return false;
                }

                canonical = number * factor;
                return true;
            }

            error = $"A {kind} parameter needs a number or a quantity string ({expected}), got JSON {value.ValueKind}.";
            return false;
        }
    }
}
