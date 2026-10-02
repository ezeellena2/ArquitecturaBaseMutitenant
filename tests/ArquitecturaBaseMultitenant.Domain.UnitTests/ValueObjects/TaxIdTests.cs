using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.ValueObjects;

internal static class TaxIdTestData
{
    internal static readonly TaxIdType Cuit = TaxIdType.Create("AR-CUIT", "AR", "CUIT",
        "99-99999999-9", "ar-cuit-mod11", "Both", isEnabled: true, sortOrder: 1);
    internal static readonly TaxIdType Dni = TaxIdType.Create("AR-DNI", "AR", "DNI",
        "99999999", "ar-dni-length", "Person", isEnabled: true, sortOrder: 2);
}

public sealed class TaxIdTests
{
    [Theory]
    [InlineData("AR", "AR-CUIT", "20-12345678-6", "20123456786")]
    [InlineData("AR", "AR-CUIT", "30-71234567-1", "30712345671")]
    [InlineData("AR", "AR-DNI", "12.345.678", "12345678")]
    public void Valid_identifications_store_country_type_and_digits_only(string country, string type,
        string input, string expectedNumber)
    {
        var catalogType = type == "AR-DNI" ? TaxIdTestData.Dni : TaxIdTestData.Cuit;
        var result = TaxId.Create(country, catalogType, input);

        Assert.True(result.IsSuccess);
        Assert.Equal(country, result.Value.Country);
        Assert.Equal(type, result.Value.Type);
        Assert.Equal(expectedNumber, result.Value.Number);
    }

    [Theory]
    [InlineData("AR", "AR-CUIT", "20-12345678-5")]
    [InlineData("AR", "AR-CUIT", "10-12345678-6")]
    [InlineData("AR", "AR-DNI", "123456")]
    [InlineData("US", "AR-CUIT", "20-12345678-6")]
    [InlineData("AR", "AR-CUIT", "20-1234567X-6")]
    public void Invalid_identifications_return_the_tax_id_error(string country, string type, string input)
    {
        var catalogType = type == "AR-CUIT" ? TaxIdTestData.Cuit : TaxIdTestData.Dni;
        var result = TaxId.Create(country, catalogType, input);

        Assert.True(result.IsFailure);
        Assert.Equal(TaxIdErrors.InvalidCode, result.Error.Code);
    }
}

public sealed class TaxIdPropertyTests
{
    [Theory]
    [InlineData("20-12345678-6")]
    [InlineData("30-71234567-1")]
    [InlineData("23-12345678-5")]
    [InlineData("27-12345678-0")]
    public void Formatting_is_removed_without_changing_a_valid_cuit(string formatted)
    {
        var created = TaxId.Create("AR", TaxIdTestData.Cuit, formatted);
        Assert.True(created.IsSuccess);

        var roundTrip = TaxId.Create(created.Value.Country, TaxIdTestData.Cuit, created.Value.Number);

        Assert.True(roundTrip.IsSuccess);
        Assert.Equal(created.Value, roundTrip.Value);
        Assert.All(roundTrip.Value.Number, character => Assert.InRange(character, '0', '9'));
    }
}
