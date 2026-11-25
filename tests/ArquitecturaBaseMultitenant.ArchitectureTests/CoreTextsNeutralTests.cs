using System.Xml.Linq;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que los recursos del núcleo no nombren a un módulo quitable: lo que es de WhatsApp o del
/// asistente vive en los recursos del módulo, así el núcleo sigue igual cuando se quita uno.
/// </summary>
public sealed class CoreTextsNeutralTests
{
    private static readonly string[] ModuleWords = ["whatsapp", "asistente", "assistant"];

    // El catálogo de permisos es del núcleo y nombra los permisos de los módulos: sus áreas, nombres y
    // descripciones son la única excepción.
    private static readonly string[] CatalogPermissionCodes =
    [
        "assistant.use",
        "assistant.configure",
        "platform.assistant.use",
        "platform.assistant.configure",
        "platform.assistant.read",
        "whatsapp.customers",
        "whatsapp.internal",
        "whatsapp.broadcasts",
        "whatsapp.manage",
        "platform.whatsapp.manage",
        "platform.whatsapp.attend",
    ];

    private static readonly HashSet<string> PermissionCatalogKeys =
    [
        "Area.assistant",
        "Area.platformAssistant",
        "Area.whatsapp",
        "Area.platformWhatsApp",
        .. CatalogPermissionCodes.SelectMany(code => new[] { $"Permission.{code}", $"PermissionDescription.{code}" }),
    ];

    [Fact]
    public void Core_resources_do_not_name_a_module()
    {
        var offenders = CoreResources()
            .SelectMany(path => Entries(path).Select(entry => (File: Path.GetFileName(path), entry.Key, entry.Value)))
            .Where(entry => !(IsPermissionCatalog(entry.File) && PermissionCatalogKeys.Contains(entry.Key)))
            .Where(entry => NamesModule(entry.Key) || NamesModule(entry.Value))
            .Select(entry => $"{entry.File}: {entry.Key}")
            .ToList();

        Assert.True(offenders.Count == 0, "Textos del núcleo que nombran a un módulo:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Every_permission_catalog_exception_exists_in_both_languages()
    {
        foreach (var file in new[] { "Permissions.resx", "Permissions.en.resx" })
        {
            var keys = Entries(Path.Combine(ResourcesRoot, file)).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
            var missing = PermissionCatalogKeys.Where(key => !keys.Contains(key)).ToList();
            Assert.True(missing.Count == 0, $"{file} no tiene: {string.Join(", ", missing)}");
        }
    }

    private static string ResourcesRoot =>
        Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Application", "Resources");

    private static IEnumerable<string> CoreResources() =>
        Directory.EnumerateFiles(ResourcesRoot, "*.resx", SearchOption.TopDirectoryOnly);

    private static IEnumerable<(string Key, string Value)> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Select(data => (data.Attribute("name")!.Value, data.Element("value")?.Value ?? string.Empty));

    private static bool IsPermissionCatalog(string file) =>
        file is "Permissions.resx" or "Permissions.en.resx";

    private static bool NamesModule(string text) =>
        ModuleWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
}
