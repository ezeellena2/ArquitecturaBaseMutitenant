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

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ??
        ResourceManager.GetString("Fallback", CultureInfo.CurrentUICulture) ?? "Registro";
}
