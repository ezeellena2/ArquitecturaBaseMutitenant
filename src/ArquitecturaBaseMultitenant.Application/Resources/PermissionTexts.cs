using System.Globalization;
using System.Resources;

namespace ArquitecturaBaseMultitenant.Application.Resources;

/// <summary>Traduce nombres y descripciones del catálogo de permisos según la cultura del pedido.</summary>
public static class PermissionTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Permissions", typeof(PermissionTexts).Assembly);

    public static string Area(string area) => Get("Area." + area);

    public static string Permission(string permission) => Get("Permission." + permission);

    public static string Description(string permission) => Get("PermissionDescription." + permission);

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
