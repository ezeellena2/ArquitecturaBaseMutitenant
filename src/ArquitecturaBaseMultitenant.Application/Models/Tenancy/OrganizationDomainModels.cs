using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

public sealed record RegisterOrganizationDomainRequest(string? Domain);

public sealed record RemoveOrganizationDomainRequest(Guid DomainId, uint? Version);

/// <summary>Un dominio de la organización con los correos verificados de ese dominio que administra.</summary>
public sealed record OrganizationDomainRow(Guid Id, string Domain, TenantDomainStatus Status,
    string VerificationToken, DateTime CreatedAtUtc, DateTime? VerifiedAtUtc, uint Version, int ManagedEmailCount);

/// <summary>Un dominio de correo de la organización; ManagedEmailCount cuenta los correos verificados de ese dominio que administra.</summary>
public sealed record OrganizationDomainResponse(Guid Id, string Domain, TenantDomainStatus Status,
    string? TxtRecordName, string? TxtRecordValue, DateTime CreatedAtUtc, DateTime? VerifiedAtUtc, uint Version,
    int ManagedEmailCount);
