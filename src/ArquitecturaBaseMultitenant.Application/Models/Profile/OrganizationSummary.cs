using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Profile;

/// <summary>Organización de la cuenta para el selector de perfiles.</summary>
public sealed record OrganizationSummary(Guid Id, string Name, string? Slug, TenantStatus Status,
    string? RoleName, MemberStatus MemberStatus)
{
    public bool IsSelectable => Status == TenantStatus.Active && MemberStatus == Domain.Tenancy.MemberStatus.Active;
}
