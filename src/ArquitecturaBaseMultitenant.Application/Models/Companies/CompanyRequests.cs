using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Companies;

/// <summary>D6: la ficha pide solo el número; el tipo sale del país de la organización.</summary>
public sealed record CreateCompanyRequest(string? Name, string? TaxIdNumber, string? TimeZoneId);

/// <summary>D6: la ficha pide solo el número; el tipo sale del país de la organización.</summary>
public sealed record UpdateCompanyRequest(Guid CompanyId, string? Name, string? TaxIdNumber,
    string? TimeZoneId, uint? Version);

public sealed record ChangeCompanyStatusRequest(Guid CompanyId, CompanyStatus? Status, uint? Version);
