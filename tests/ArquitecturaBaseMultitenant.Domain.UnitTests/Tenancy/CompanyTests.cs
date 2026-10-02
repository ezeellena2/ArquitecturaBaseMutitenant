using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Tenancy;

public sealed class CompanyTests
{
    [Fact]
    public void Create_trims_required_values_and_allows_tax_id_to_be_absent()
    {
        var company = Company.Create("  Taller Norte  ", taxId: null, " America/Argentina/Buenos_Aires ");

        Assert.True(company.IsSuccess);
        Assert.Equal("Taller Norte", company.Value.Name);
        Assert.Equal("America/Argentina/Buenos_Aires", company.Value.TimeZoneId);
        Assert.Null(company.Value.TaxId);
    }

    [Fact]
    public void Create_rejects_missing_or_oversized_names_and_missing_time_zone()
    {
        Assert.Equal(CompanyErrors.NameRequiredCode, Company.Create("  ", null, "UTC").Error.Code);
        Assert.Equal(CompanyErrors.NameTooLongCode,
            Company.Create(new string('x', 121), null, "UTC").Error.Code);
        Assert.Equal(CompanyErrors.TimeZoneRequiredCode,
            Company.Create("Taller Norte", null, " ").Error.Code);
    }

    [Fact]
    public void Update_replaces_name_tax_id_and_zone_as_one_valid_change()
    {
        var taxType = TaxIdType.Create("AR-CUIT", "AR", "CUIT", "00-00000000-0",
            "ar-cuit-mod11", "Company", isEnabled: true, sortOrder: null);
        var taxId = TaxId.Create("AR", taxType, "30712345671");
        Assert.True(taxId.IsSuccess);
        var company = Company.Create("Taller Norte", null, "UTC").Value;

        var updated = company.Update("Taller Sur", taxId.Value, "America/Argentina/Buenos_Aires");

        Assert.True(updated.IsSuccess);
        Assert.Equal("Taller Sur", company.Name);
        Assert.Equal(taxId.Value, company.TaxId);
        Assert.Equal("America/Argentina/Buenos_Aires", company.TimeZoneId);
    }

    [Fact]
    public void Create_can_keep_a_stable_id_for_idempotent_provisioning()
    {
        var id = Guid.Parse("20000000-0000-0000-0000-000000000044");

        var company = Company.Create(id, "Acme", null, "UTC");

        Assert.True(company.IsSuccess);
        Assert.Equal(id, company.Value.Id);
    }
}
