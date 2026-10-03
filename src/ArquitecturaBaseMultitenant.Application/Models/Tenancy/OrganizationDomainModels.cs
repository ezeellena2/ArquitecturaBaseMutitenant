using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

public sealed record RegisterOrganizationDomainRequest(string? Domain);

public sealed record RemoveOrganizationDomainRequest(Guid DomainId, uint? Version);

public sealed record OrganizationDomainRow(Guid Id, string Domain, TenantDomainStatus Status,
    string VerificationToken, DateTime CreatedAtUtc, DateTime? VerifiedAtUtc, uint Version);

public sealed record OrganizationDomainResponse(Guid Id, string Domain, TenantDomainStatus Status,
    string? TxtRecordName, string? TxtRecordValue, DateTime CreatedAtUtc, DateTime? VerifiedAtUtc, uint Version);
