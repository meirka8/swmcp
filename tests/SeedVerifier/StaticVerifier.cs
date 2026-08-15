using System.Reflection;
using System.Text.Json;
using SolidWorks.Interop.sldworks;

namespace SeedVerifier;

public sealed record SeedSpec(string Name, string Member, object?[]? Args)
{
    public int ArgCount => Args?.Length ?? 0;
    public string Describe() => Args is { Length: > 0 }
        ? $"{Member}({string.Join(", ", Args.Select(a => a?.ToString()?.ToLowerInvariant() ?? "null"))})"
        : Member;
}

public enum Verdict { Ok, WrongShape, Missing, NoInterface }

public sealed record SpecResult(string FeatureType, SeedSpec Spec, Verdict Verdict, string Detail);

/// <summary>
/// Phase 1: checks every researched spec against the actual public member surface
/// of the interop interface the research report names, with no SolidWorks instance
/// required. Uses the same SolidWorks.Interop.sldworks assembly SwBridge binds to.
/// </summary>
public static class StaticVerifier
{
    public static Dictionary<string, List<SeedSpec>> LoadSeed(string path)
    {
        var result = new Dictionary<string, List<SeedSpec>>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var type in doc.RootElement.EnumerateObject())
        {
            var specs = new List<SeedSpec>();
            foreach (var entry in type.Value.EnumerateArray())
            {
                var name = entry.GetProperty("name").GetString()!;
                var member = entry.TryGetProperty("member", out var m) ? m.GetString()! : name;
                object?[]? args = null;
                if (entry.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array)
                {
                    args = a.EnumerateArray().Select(ToClr).ToArray();
                }
                specs.Add(new SeedSpec(name, member, args));
            }
            result[type.Name] = specs;
        }
        return result;
    }

    private static object? ToClr(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt32(out var i) ? i : e.GetDouble(),
        _ => null,
    };

    public static Type? FindInteropType(string interfaceName) =>
        typeof(ISldWorks).Assembly.GetTypes()
            .FirstOrDefault(t => t.IsInterface && string.Equals(t.Name, interfaceName, StringComparison.Ordinal));

    /// <summary>All readable member candidates on an interface, as (name, paramCount, signature).</summary>
    public static List<(string Name, int ParamCount, string Signature, bool IsProperty)> ReadableMembers(Type t)
    {
        var list = new List<(string, int, string, bool)>();
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var g = p.GetGetMethod();
            if (g == null) continue;
            var ps = g.GetParameters();
            list.Add((p.Name, ps.Length, $"{p.PropertyType.Name} {p.Name}" + (ps.Length > 0 ? $"[{Params(ps)}]" : " {get}"), true));
        }
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.IsSpecialName) continue;
            var ps = m.GetParameters();
            list.Add((m.Name, ps.Length, $"{m.ReturnType.Name} {m.Name}({Params(ps)})", false));
        }
        return list;
    }

    private static string Params(ParameterInfo[] ps) =>
        string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"));

    public static List<SpecResult> Verify(string featureType, IReadOnlyList<SeedSpec> specs, string interfaceName)
    {
        var results = new List<SpecResult>();
        var t = FindInteropType(interfaceName);
        if (t == null)
        {
            foreach (var s in specs)
                results.Add(new SpecResult(featureType, s, Verdict.NoInterface,
                    $"interface '{interfaceName}' not found in SolidWorks.Interop.sldworks"));
            return results;
        }

        var members = ReadableMembers(t);
        foreach (var spec in specs)
        {
            var byName = members.Where(m => string.Equals(m.Name, spec.Member, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 0)
            {
                // near misses: any member whose name contains the spec member or vice versa
                var near = members
                    .Where(m => m.Name.Contains(spec.Member, StringComparison.OrdinalIgnoreCase) ||
                                spec.Member.Contains(m.Name, StringComparison.OrdinalIgnoreCase) ||
                                Overlaps(m.Name, spec.Member))
                    .Select(m => m.Signature)
                    .Distinct()
                    .Take(8)
                    .ToList();
                results.Add(new SpecResult(featureType, spec, Verdict.Missing,
                    near.Count > 0 ? "near: " + string.Join(" | ", near) : "no near-miss members"));
                continue;
            }

            var match = byName.FirstOrDefault(m => m.ParamCount == spec.ArgCount);
            if (match != default)
            {
                results.Add(new SpecResult(featureType, spec, Verdict.Ok,
                    byName.First(m => m.ParamCount == spec.ArgCount).Signature));
            }
            else
            {
                results.Add(new SpecResult(featureType, spec, Verdict.WrongShape,
                    $"want {spec.ArgCount} arg(s); actual: " + string.Join(" | ", byName.Select(m => m.Signature))));
            }
        }
        return results;
    }

    private static bool Overlaps(string a, string b)
    {
        // crude token overlap so "DefaultRadius" surfaces "Radius"-ish members
        var tokens = SplitCamel(b);
        return tokens.Any(tok => tok.Length >= 4 && a.Contains(tok, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitCamel(string s)
    {
        var cur = "";
        foreach (var c in s)
        {
            if (char.IsUpper(c) && cur.Length > 0) { yield return cur; cur = c.ToString(); }
            else cur += c;
        }
        if (cur.Length > 0) yield return cur;
    }
}
