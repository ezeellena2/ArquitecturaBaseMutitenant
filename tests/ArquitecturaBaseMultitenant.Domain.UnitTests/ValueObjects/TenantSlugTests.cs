using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.ValueObjects;

public sealed class TenantSlugTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("abc")]
    [InlineData("mi-empresa-1")]
    public void Accepts_slugs_matching_the_registration_board(string input)
    {
        var result = TenantSlug.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(input, result.Value.Value);
    }

    [Fact]
    public void Accepts_sixty_three_characters_and_rejects_sixty_four()
    {
        Assert.True(TenantSlug.Create(new string('a', 63)).IsSuccess);
        Assert.True(TenantSlug.Create(new string('a', 64)).IsFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("-empresa")]
    [InlineData("empresa-")]
    [InlineData("mi_empresa")]
    [InlineData("área")]
    public void Rejects_slugs_outside_the_registration_board_pattern(string input)
    {
        var result = TenantSlug.Create(input);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantSlugErrors.InvalidCode, result.Error.Code);
    }

    [Fact]
    public void Normalizes_case_and_surrounding_whitespace()
    {
        var result = TenantSlug.Create("  Mi-Empresa-1  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("mi-empresa-1", result.Value.Value);
    }
}
