using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Resources;

/// <summary>Traduce únicamente roles de sistema; los nombres personalizados se conservan como los eligió la organización.</summary>
public static class RoleTexts
{
    private static readonly ResourceManager Resources = new(
        "ArquitecturaBaseMultitenant.Application.Resources.RoleNames", typeof(RoleTexts).Assembly);

    public static string Name(string name) => name switch
    {
        SystemRoles.TenantAdmin => Resources.GetString("SystemRole.TenantAdmin", CultureInfo.CurrentUICulture) ?? name,
        SystemRoles.CompanyAdmin => Resources.GetString("SystemRole.CompanyAdmin", CultureInfo.CurrentUICulture) ?? name,
        _ => name,
    };
}
