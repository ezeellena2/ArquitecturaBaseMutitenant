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

    public static string Summary(AuditAction action, string entityType, string? affectedDisplayName)
    {
        if (string.IsNullOrWhiteSpace(affectedDisplayName)) return $"{Action(action)} {Entity(entityType)}";

        var key = entityType switch
        {
            "RoleAssignment" => "AuditSummary.RoleAssignment",
            "CompanyMembership" => "AuditSummary.CompanyMembership",
            "Member" => "AuditSummary.Member." + action,
            "Invitation" => "AuditSummary.Invitation." + action,
            "Company" => "AuditSummary.Company." + action,
            "Role" => "AuditSummary.Role." + action,
            _ => string.Empty,
        };
        var template = string.IsNullOrEmpty(key)
            ? null
            : ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        return template is null
            ? $"{Action(action)} {Entity(entityType)} · {affectedDisplayName}"
            : template.Replace("{0}", affectedDisplayName, StringComparison.Ordinal);
    }

    public static string WholeOrganization() => Get("Location.Organization");

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ??
        ResourceManager.GetString("Fallback", CultureInfo.CurrentUICulture) ?? "Registro";
}
