using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Empresa administrada dentro de una organización; sus datos siguen el tenant activo.</summary>
public sealed class Company : Entity, ITenantOwned, IAuditable, IVersioned
{
    private Company()
    {
        Name = string.Empty;
        TimeZoneId = string.Empty;
    }

    private Company(string name, TaxId? taxId, string timeZoneId)
    {
        Name = name;
        SetTaxId(taxId);
        TimeZoneId = timeZoneId;
    }

    public string Name { get; private set; }

    public string? TaxCountry { get; private set; }

    public string? TaxType { get; private set; }

    public string? TaxNumber { get; private set; }

    public TaxId? TaxId => TaxCountry is null || TaxType is null || TaxNumber is null
        ? null : TaxId.Restore(TaxCountry, TaxType, TaxNumber);

    public string TimeZoneId { get; private set; }

    public Guid TenantId { get; private set; }

    public uint Version { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime? ModifiedAtUtc { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static Result<Company> Create(string? name, TaxId? taxId, string? timeZoneId)
    {
        var validation = Validate(name, timeZoneId);
        if (validation.IsFailure) return validation.Error;
        return new Company(name!.Trim(), taxId, timeZoneId!.Trim());
    }

    public Result Update(string? name, TaxId? taxId, string? timeZoneId)
    {
        var validation = Validate(name, timeZoneId);
        if (validation.IsFailure) return validation;
        Name = name!.Trim();
        SetTaxId(taxId);
        TimeZoneId = timeZoneId!.Trim();
        return Result.Success();
    }

    private static Result Validate(string? name, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(name)) return CompanyErrors.NameRequired;
        if (name.Trim().Length > TextLimits.OrganizationName) return CompanyErrors.NameTooLong;
        if (string.IsNullOrWhiteSpace(timeZoneId)) return CompanyErrors.TimeZoneRequired;
        return Result.Success();
    }

    private void SetTaxId(TaxId? taxId)
    {
        TaxCountry = taxId?.Country;
        TaxType = taxId?.Type;
        TaxNumber = taxId?.Number;
    }
}
