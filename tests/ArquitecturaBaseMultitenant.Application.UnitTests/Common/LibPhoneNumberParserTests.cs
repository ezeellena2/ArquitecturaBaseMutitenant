using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

/// <summary>Interpreta lo que escribe una persona con el país elegido y devuelve E.164 (docs/rules/telefonos.md).</summary>
public sealed class LibPhoneNumberParserTests
{
    private readonly LibPhoneNumberParser parser = new();

    [Theory]
    [InlineData("AR", "011 4321-5678", PhoneUsage.Any, "+541143215678")]
    [InlineData("AR", "11 4321 5678", PhoneUsage.Any, "+541143215678")]
    [InlineData("AR", "011 15 4321-5678", PhoneUsage.Mobile, "+5491143215678")]
    [InlineData("AR", "11 4321 5678", PhoneUsage.Mobile, "+5491143215678")]
    [InlineData("AR", "+54 9 11 4321 5678", PhoneUsage.Any, "+5491143215678")]
    [InlineData("UY", "+54 9 11 4321 5678", PhoneUsage.Mobile, "+5491143215678")]
    [InlineData("ES", "612 34 56 78", PhoneUsage.Mobile, "+34612345678")]
    [InlineData("ES", "912 34 56 78", PhoneUsage.Any, "+34912345678")]
    public void Parses_what_a_person_types_into_e164(string country, string number, PhoneUsage usage, string expected)
    {
        var parsed = parser.Parse(country, number, usage);

        Assert.True(parsed.IsSuccess, parsed.IsFailure ? parsed.Error.Code : null);
        Assert.Equal(expected, parsed.Value.Value);
    }

    [Theory]
    [InlineData("AR", "123")]
    [InlineData("AR", "")]
    [InlineData(null, "11 4321 5678")]
    [InlineData("ZZ", "11 4321 5678")]
    [InlineData("AR", "no es un número")]
    public void Rejects_a_number_that_is_not_valid(string? country, string number)
    {
        var parsed = parser.Parse(country, number, PhoneUsage.Any);

        Assert.True(parsed.IsFailure);
        Assert.Equal(PhoneErrors.InvalidCode, parsed.Error.Code);
    }

    [Fact]
    public void A_landline_is_not_accepted_where_a_mobile_is_required()
    {
        var parsed = parser.Parse("ES", "912 34 56 78", PhoneUsage.Mobile);

        Assert.True(parsed.IsFailure);
        Assert.Equal(PhoneErrors.NotMobileCode, parsed.Error.Code);
    }

    [Fact]
    public void Masks_the_middle_of_the_number_for_logs()
    {
        Assert.Equal("+54 9 11 •••• 6789", parser.Mask(PhoneNumber.Create("+5491143216789").Value));
    }
}
