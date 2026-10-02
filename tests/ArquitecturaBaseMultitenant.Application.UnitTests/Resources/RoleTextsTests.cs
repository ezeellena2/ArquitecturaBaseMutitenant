using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Resources;

public sealed class RoleTextsTests
{
    [Theory]
    [InlineData("es", SystemRoles.TenantAdmin, "Dueño")]
    [InlineData("en", SystemRoles.TenantAdmin, "Owner")]
    [InlineData("es", SystemRoles.CompanyAdmin, "Administrador")]
    [InlineData("en", SystemRoles.CompanyAdmin, "Administrator")]
    public void System_role_names_are_translated(string cultureName, string roleName, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.Equal(expected, RoleTexts.Name(roleName));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Custom_role_names_are_preserved()
    {
        Assert.Equal("Operaciones", RoleTexts.Name("Operaciones"));
    }
}
