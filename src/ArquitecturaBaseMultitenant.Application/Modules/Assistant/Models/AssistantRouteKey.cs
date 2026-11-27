using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

// Los enlaces del asistente se guardan con este valor como número (LinksJson): uno nuevo va siempre al final, así lo guardado
// se sigue leyendo igual (migración, «Convivencia de versiones»).
public enum AssistantRouteKey
{
    [JsonStringEnumMemberName("business.users")] BusinessUsers,
    [JsonStringEnumMemberName("business.roles")] BusinessRoles,
    [JsonStringEnumMemberName("business.companies")] BusinessCompanies,
    [JsonStringEnumMemberName("business.audit")] BusinessAudit,
    [JsonStringEnumMemberName("business.home")] BusinessHome,
    [JsonStringEnumMemberName("platform.organizations")] PlatformOrganizations,
    [JsonStringEnumMemberName("platform.accounts")] PlatformAccounts,
    [JsonStringEnumMemberName("platform.recoveries")] PlatformRecoveries,
    [JsonStringEnumMemberName("platform.audit")] PlatformAudit,
    [JsonStringEnumMemberName("platform.assistant.usage")] PlatformAssistantUsage,
    [JsonStringEnumMemberName("personal.account")] PersonalAccount,
    [JsonStringEnumMemberName("personal.organizations")] PersonalOrganizations,
    [JsonStringEnumMemberName("business.forms.catalogs")] BusinessFormCatalogs,
}

/// <summary>
/// Traduce cada pantalla del núcleo donde se confirma una acción (<see cref="AssistantActionScreen"/>, ADR 0068 §2) a la ruta del
/// módulo, con su acceso y su permiso de lectura. Una pantalla nueva suma su valor y su traducción.
/// </summary>
public static class AssistantActionScreenRoutes
{
    public static AssistantRouteKey RouteOf(AssistantActionScreen screen) => screen switch
    {
        AssistantActionScreen.InviteUser => AssistantRouteKey.BusinessHome,
        AssistantActionScreen.UserDetail => AssistantRouteKey.BusinessUsers,
        AssistantActionScreen.RoleEditor or AssistantActionScreen.Roles => AssistantRouteKey.BusinessRoles,
        AssistantActionScreen.CatalogEditor => AssistantRouteKey.BusinessFormCatalogs,
        _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, "Pantalla sin traducción."),
    };
}
