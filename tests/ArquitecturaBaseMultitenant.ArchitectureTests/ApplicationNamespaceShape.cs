namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Los módulos conservan la misma forma de Services/Interfaces del núcleo.</summary>
internal static class ApplicationNamespaceShape
{
    public static bool IsServices(Type type) => InArea(type.Namespace, "Services");
    public static bool IsServiceContract(Type type) => type.IsInterface
        && type.Name.StartsWith('I') && type.Name.EndsWith("Service", StringComparison.Ordinal)
        && InArea(type.Namespace, "Interfaces.Services");
    public static bool IsInterface(Type type) => InArea(type.Namespace, "Interfaces");
    public static bool IsIntegrationAdapter(Type type) => type.GetInterfaces()
        .Any(contract => contract.Namespace?.StartsWith("ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.",
            StringComparison.Ordinal) == true);
    private static bool InArea(string? value, string area)
    {
        const string root = "ArquitecturaBaseMultitenant.Application.";
        if (value is null || !value.StartsWith(root, StringComparison.Ordinal)) return false;
        var tail = value[root.Length..];
        if (tail.StartsWith("Modules.", StringComparison.Ordinal))
        {
            var separator = tail.IndexOf('.', "Modules.".Length);
            if (separator < 0) return false;
            tail = tail[(separator + 1)..];
        }
        return tail == area || tail.StartsWith(area + ".", StringComparison.Ordinal);
    }
}
