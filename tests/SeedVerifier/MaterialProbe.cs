using SolidWorks.Interop.sldworks;
using SwBridge;

namespace SeedVerifier;

/// <summary>
/// Ad-hoc live probe for material-name read variants against an already-open
/// document — used once to determine which member/argument shape actually
/// returns a non-empty material name after set_material has been applied,
/// since IPartDoc.GetMaterialPropertyName2's exact ConfigName argument was
/// not something the static interop signature alone could answer.
/// </summary>
internal static class MaterialProbe
{
    public static int Run(string docName)
    {
        var connection = new SwConnection();
        var docs = new DocumentManager(connection);
        var doc = docs.Resolve(docName);
        if (doc == null)
        {
            Console.WriteLine($"'{docName}' not open");
            return 1;
        }

        void TryCall(string label, string member, object?[] args)
        {
            var ok = ComPropertyReader.TryGetMember(doc.Model, member, args, out var value);
            Console.WriteLine($"{label}: ok={ok} value='{value}' ({value?.GetType().Name})");
        }

        TryCall("GetMaterialPropertyName2('', '')", "GetMaterialPropertyName2", new object?[] { "", "" });
        TryCall("GetMaterialPropertyName2('Default', '')", "GetMaterialPropertyName2", new object?[] { "Default", "" });
        TryCall("GetMaterialPropertyName2('') [Database omitted]", "GetMaterialPropertyName2", new object?[] { "" });
        TryCall("GetMaterialPropertyName() [Database omitted]", "GetMaterialPropertyName", Array.Empty<object?>());
        TryCall("GetMaterialPropertyName2('', null)", "GetMaterialPropertyName2", new object?[] { "", null });

        // Active configuration name, read generically.
        var configOk = ComPropertyReader.TryGetMember(doc.Model, "IGetActiveConfiguration", null, out var configValue);
        string? configName = null;
        if (configOk && configValue != null)
        {
            ComPropertyReader.TryGetProperty(configValue, "Name", out var configNameValue);
            configName = configNameValue as string;
            Console.WriteLine($"active configuration name: '{configName}'");
        }
        else
        {
            Console.WriteLine($"IGetActiveConfiguration: ok={configOk} value={configValue}");
        }

        if (configName != null)
        {
            TryCall($"GetMaterialPropertyName2('{configName}', '')", "GetMaterialPropertyName2", new object?[] { configName, "" });
        }

        TryCall("GetMaterialPropertyName('')", "GetMaterialPropertyName", new object?[] { "" });

        // MaterialIdName via IPartDoc directly (early-bound cast) as ground truth.
        if (doc.Model is PartDoc partDoc)
        {
            var name2 = partDoc.GetMaterialPropertyName2("", out var db);
            Console.WriteLine($"early-bound GetMaterialPropertyName2(\"\", out db) -> name='{name2}' db='{db}'");

            var name = partDoc.GetMaterialPropertyName(out var db2);
            Console.WriteLine($"early-bound GetMaterialPropertyName(out db) -> name='{name}' db='{db2}'");
        }

        return 0;
    }
}
