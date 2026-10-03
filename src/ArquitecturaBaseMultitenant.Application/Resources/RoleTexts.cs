using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Resources;

/// <summary>Traduce únicamente roles de sistema; los nombres personalizados se conservan como los eligió la organización.</summary>
public static class RoleTexts
{
    private static readonly ResourceManager Resources = new(
        "ArquitecturaBaseMultitenant.Application.Resources.RoleNames", typeof(RoleTexts).Assembly);

    public static string Name(string name) => NameIn(name, CultureInfo.CurrentUICulture);

    /// <summary>Para mensajes que salen en el idioma de quien los recibe y no en el de la petición.</summary>
    public static string NameIn(string name, CultureInfo culture) => name switch
    {
        SystemRoles.TenantAdmin => Resources.GetString("SystemRole.TenantAdmin", culture) ?? name,
        SystemRoles.CompanyAdmin => Resources.GetString("SystemRole.CompanyAdmin", culture) ?? name,
        _ => name,
    };
}
