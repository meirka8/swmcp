using System.Reflection;
using SolidWorks.Interop.sldworks;

namespace SeedVerifier;

public static class FindMember
{
    public static void Run(string needle)
    {
        foreach (var t in typeof(ISldWorks).Assembly.GetTypes().Where(t => t.IsInterface))
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.IsSpecialName) continue;
                if (!m.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;
                Console.WriteLine($"{t.Name}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {m.ReturnType.Name}");
            }
        }
    }
}
