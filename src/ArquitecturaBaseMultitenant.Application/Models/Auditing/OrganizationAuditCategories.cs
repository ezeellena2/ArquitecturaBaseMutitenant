using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Auditing;

/// <summary>Categorías del filtro de auditoría y entidades que incluye cada una.</summary>
public static class OrganizationAuditCategories
{
    public static readonly IReadOnlyDictionary<string, string[]> EntityTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["users"] = [nameof(Member), nameof(Invitation)],
            ["roles"] = [nameof(Role), nameof(RoleAssignment)],
            ["companies"] = [nameof(Company), nameof(CompanyMembership)],
            ["settings"] = [nameof(TenantSettings), nameof(TenantDomain)],
            ["organization"] = [nameof(PublicPage), nameof(Tenant)],
        }.AsReadOnly();
}
