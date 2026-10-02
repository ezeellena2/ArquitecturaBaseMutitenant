using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Companies;

public sealed record CompanyResponse(Guid Id, string Name, CompanyTaxIdResponse? TaxId, string TimeZoneId,
    CompanyStatus Status, int MemberCount, uint Version, DateTime CreatedAtUtc);

public sealed record CompanyTaxIdResponse(string Country, string Type, string Number);

public sealed record CompanyRow(Guid Id, string Name, string? TaxCountry, string? TaxType, string? TaxNumber,
    string TimeZoneId, CompanyStatus Status, int MemberCount, uint Version, DateTime CreatedAtUtc);

public sealed record CompanyFilterCountsResponse(int All, int Active, int Inactive);
