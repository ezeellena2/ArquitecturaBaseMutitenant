using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Tenancy;

public sealed class ReservedTenantSlugTests
{
    [Fact]
    public void Defaults_match_the_validated_business_signup_board()
    {
        Assert.Equal(
            new[] { "admin", "api", "app", "auth", "empresas", "plataforma", "www" },
            ReservedTenantSlug.Defaults.Select(item => item.Slug).Order(StringComparer.Ordinal));
    }
}
