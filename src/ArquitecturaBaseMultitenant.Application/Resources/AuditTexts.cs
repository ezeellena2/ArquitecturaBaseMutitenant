using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Resources;

public static class AuditTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Audit", typeof(AuditTexts).Assembly);

    public static string Action(AuditAction action) => Get("AuditAction." + action);

    public static string Entity(string entityType) => Get("Entity." + entityType);

    public static string Actor(AuditActorKind actorKind) => Get("Actor." + actorKind);

    public static string Field(string field) => Get("Field." + field);

    /// <summary>El nombre de un campo del detalle; null si no tiene, y entonces el campo no se muestra.</summary>
    public static string? FieldOrNull(string field) =>
        ResourceManager.GetString("Field." + field, CultureInfo.CurrentUICulture);

    /// <summary>Un estado en palabras de las pantallas («Deshabilitado»); null si no tiene.</summary>
    public static string? Status(string entityType, string status) =>
        ResourceManager.GetString($"Value.{entityType}.Status.{status}", CultureInfo.CurrentUICulture);

    /// <summary>
    /// «Qué» de una entrada: <c>AuditSummary.{entidad}.{variante}</c>, después <c>.{acción}</c> y después el de la
    /// entidad. La variante nombra un cambio más preciso que la acción, como «Deshabilitó a {0}» en lugar de «Cambió
    /// los datos de {0}», o un campo solo, como «Cambió la zona de {0}». Un texto con {0} pide el nombre de lo afectado;
    /// sin nombre, se salta. Sin ningún texto, «{acción} {entidad}».
    /// </summary>
    public static string Summary(AuditAction action, string entityType, string? affectedDisplayName,
        string? variant = null)
    {
        var named = !string.IsNullOrWhiteSpace(affectedDisplayName);
        string?[] keys =
        [
            variant is null ? null : $"AuditSummary.{entityType}.{variant}",
            $"AuditSummary.{entityType}.{action}",
            $"AuditSummary.{entityType}",
        ];
        foreach (var key in keys.OfType<string>())
        {
            if (ResourceManager.GetString(key, CultureInfo.CurrentUICulture) is not { } template) continue;
            if (!template.Contains("{0}", StringComparison.Ordinal)) return template;
            if (named) return template.Replace("{0}", affectedDisplayName, StringComparison.Ordinal);
        }

        return named
            ? $"{Action(action)} {Entity(entityType)} · {affectedDisplayName}"
            : $"{Action(action)} {Entity(entityType)}";
    }

    public static string WholeOrganization() => Get("Location.Organization");
    public static string NoRoles() => Get("Value.NoRoles");
    public static string OrganizationCreationReason() => Get("Platform.OrganizationCreationReason");
    public static string OrganizationRetryReason() => Get("Platform.OrganizationRetryReason");
    public static string PublicPageMetadataReadReason() => Get("Platform.PublicPageMetadataReadReason");
    public static string DomainMetadataReadReason() => Get("Platform.DomainMetadataReadReason");
    public static string OperatorInvitationReason() => Get("Platform.OperatorInvitationReason");
    public static string OperatorInvitationResendReason() => Get("Platform.OperatorInvitationResendReason");
    public static string OperatorInvitationRevokeReason() => Get("Platform.OperatorInvitationRevokeReason");
    public static string LegalPublicationReason() => Get("Platform.LegalPublicationReason");
    public static string SecurityEvent(SecurityEventType type) => Get("SecurityEvent." + type);
    public static string SignIn(ArquitecturaBaseMultitenant.Domain.Authentication.LoginAuditMethod method) => Get("SignIn." + method);
    public static string SignInFailed(string reason) => Get("SignIn.Failed").Replace("{0}", reason, StringComparison.Ordinal);
    public static string ActorRole(AuditActorKind actorKind) => Get("ActorRole." + actorKind);
    public static string PlatformTarget() => Get("Target.Platform");
    public static string UnknownAccount() => Get("Target.UnknownAccount");

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ??
        ResourceManager.GetString("Fallback", CultureInfo.CurrentUICulture) ?? "Registro";
}
