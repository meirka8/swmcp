namespace SeedVerifier;

public static class EnumDump
{
    public static void Dump(string[] patterns)
    {
        var asm = typeof(SolidWorks.Interop.swconst.swBodyType_e).Assembly;
        foreach (var t in asm.GetTypes().Where(t => t.IsEnum))
        {
            if (!patterns.Any(p => t.Name.Contains(p, StringComparison.OrdinalIgnoreCase))) continue;
            Console.WriteLine($"=== {t.Name} ===");
            foreach (var n in Enum.GetNames(t))
                Console.WriteLine($"    {n} = {Convert.ToInt64(Enum.Parse(t, n))}");
        }
    }
}
