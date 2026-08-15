using SeedVerifier;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "static";
        var seedPath = args.Length > 1
            ? args[1]
            : @"C:\projects\aibuilds\docs\known_features.researched.json";

        switch (mode)
        {
            case "static":
                RunStatic(seedPath);
                return 0;
            case "members":
                RunMembers(args.Skip(1).ToArray());
                return 0;
            case "find":
                FindMember.Run(args[1]);
                return 0;
            case "enums":
                EnumDump.Dump(args.Skip(1).ToArray());
                return 0;
            case "zoo":
                return Zoo.Run(seedPath);
            case "open":
                return Zoo.OpenDoc(args[1]);
            case "close":
                return Zoo.CloseDoc(args[1]);
            case "inspect":
                return Zoo.InspectOpenDoc(args.Length > 1 ? args[1] : "FeatureZoo", seedPath);
            default:
                Console.WriteLine("modes: static [seed.json] | members <IFaceName>... | zoo [seed.json] | inspect <docName> [seed.json]");
                return 2;
        }
    }

    private static void RunStatic(string seedPath)
    {
        var seed = StaticVerifier.LoadSeed(seedPath);
        Console.WriteLine($"=== PHASE 1: static signature verification ===");
        Console.WriteLine($"seed: {seedPath}");
        Console.WriteLine($"interop assembly: {typeof(SolidWorks.Interop.sldworks.ISldWorks).Assembly.FullName}");
        Console.WriteLine();

        int ok = 0, wrong = 0, missing = 0, noiface = 0;
        foreach (var (featureType, specs) in seed)
        {
            var ifaceName = InterfaceMap.Researched.TryGetValue(featureType, out var n) ? n : "(unmapped)";
            var t = StaticVerifier.FindInteropType(ifaceName);
            Console.WriteLine($"--- {featureType} -> {ifaceName} [{(t == null ? "NOT FOUND" : "found")}]");
            foreach (var r in StaticVerifier.Verify(featureType, specs, ifaceName))
            {
                switch (r.Verdict)
                {
                    case Verdict.Ok: ok++; break;
                    case Verdict.WrongShape: wrong++; break;
                    case Verdict.Missing: missing++; break;
                    case Verdict.NoInterface: noiface++; break;
                }
                Console.WriteLine($"    [{r.Verdict,-11}] {r.Spec.Describe(),-32} {r.Detail}");
            }

            if (t != null)
            {
                var all = StaticVerifier.ReadableMembers(t)
                    .Select(m => m.Signature).Distinct().OrderBy(s => s).ToList();
                Console.WriteLine($"    ALL READABLE MEMBERS ({all.Count}):");
                foreach (var s in all) Console.WriteLine($"        {s}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"=== TOTALS: OK={ok} WRONG-SHAPE={wrong} MISSING={missing} NO-INTERFACE={noiface} ===");
    }

    private static void RunMembers(string[] names)
    {
        foreach (var name in names)
        {
            var t = StaticVerifier.FindInteropType(name);
            if (t == null) { Console.WriteLine($"{name}: NOT FOUND"); continue; }
            Console.WriteLine($"=== {name} ===");
            foreach (var m in StaticVerifier.ReadableMembers(t).Select(m => m.Signature).Distinct().OrderBy(s => s))
                Console.WriteLine($"    {m}");
            Console.WriteLine();
        }
    }
}

