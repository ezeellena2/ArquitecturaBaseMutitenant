using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Las etiquetas del inventario de ADR 0052 y qué cambio rastreado avanza cada una. Las de membresías, roles,
/// asignaciones y empresas son por organización: ninguna lectura depende de un número creciente de etiquetas por persona.
/// </summary>
internal static class CacheTags
{
    internal const int MaxLength = 100;

    /// <summary>
    /// La identidad de la base: un número al azar que fija la migración y nada avanza. Entra en toda clave, así una base
    /// nueva o recreada nunca lee lo que otra dejó en el mismo Redis.
    /// </summary>
    internal static string Database { get; } = CacheKeys.Platform("database");

    internal static string PlatformConfiguration { get; } = CacheKeys.Platform("settings");

    internal static string References { get; } = CacheKeys.Platform("ref");

    internal static string Organization(Guid tenantId) => CacheKeys.Tenant(tenantId, "org");

    internal static string Members(Guid tenantId) => CacheKeys.Tenant(tenantId, "members");

    internal static string Roles(Guid tenantId) => CacheKeys.Tenant(tenantId, "roles");

    internal static string Assignments(Guid tenantId) => CacheKeys.Tenant(tenantId, "assignments");

    internal static string Companies(Guid tenantId) => CacheKeys.Tenant(tenantId, "companies");

    internal static string Features(Guid tenantId) => CacheKeys.Tenant(tenantId, "features");

    internal static string Settings(Guid tenantId) => CacheKeys.Tenant(tenantId, "settings");

    internal static string Account(Guid userId) => CacheKeys.User(userId, "account");

    /// <summary>Las etiquetas que avanza un alta, un cambio o una baja rastreada; ninguna si la entidad no alimenta una caché.</summary>
    internal static IEnumerable<string> Changed(EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) return [];
        return entry.Entity switch
        {
            // El nombre de la organización viaja en sus preferencias cacheadas.
            Tenant tenant => [Organization(tenant.Id), Settings(tenant.Id)],
            // De la cuenta, una caché sólo lee su estado: el sello o los intentos de ingreso no la cambian.
            ApplicationUser user when entry.State != EntityState.Modified
                || entry.Property(nameof(ApplicationUser.Status)).IsModified => [Account(user.Id)],
            Member member => [Members(member.TenantId)],
            Role role => [Roles(role.TenantId)],
            RoleAssignment assignment => [Assignments(assignment.TenantId)],
            Company company => [Companies(company.TenantId)],
            CompanyMembership membership => [Companies(membership.TenantId)],
            TenantFeature feature => [Features(feature.TenantId)],
            TenantSettings settings => [Settings(settings.TenantId)],
            PlatformSettings => [PlatformConfiguration],
            _ when entry.Metadata.ClrType.Namespace == typeof(Currency).Namespace => [References],
            _ => [],
        };
    }
}
