using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Companies;

public sealed record CreateCompanyRequest(string? Name, string? TaxIdType, string? TaxIdNumber, string? TimeZoneId);

public sealed record UpdateCompanyRequest(Guid CompanyId, string? Name, string? TaxIdType, string? TaxIdNumber,
    string? TimeZoneId, uint? Version);

public sealed record ChangeCompanyStatusRequest(Guid CompanyId, CompanyStatus? Status, uint? Version);
