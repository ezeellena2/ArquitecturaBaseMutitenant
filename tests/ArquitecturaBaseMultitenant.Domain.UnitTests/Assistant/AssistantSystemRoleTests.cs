using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Assistant;

public sealed class AssistantSystemRoleTests
{
    [Fact]
    public void Installed_assistant_grants_use_to_both_admins_and_configure_only_to_the_owner()
    {
        var tenantAdmin = SystemRoles.CreateTenantAdmin();
        var companyAdmin = SystemRoles.CreateCompanyAdmin();
        Assert.Equal([.. Permissions.CompanyScoped, Permissions.Assistant.Use], companyAdmin.Permissions);
        Assert.Contains(Permissions.Assistant.Configure, tenantAdmin.Permissions);
        Assert.DoesNotContain(Permissions.Assistant.Configure, companyAdmin.Permissions);
        Assert.DoesNotContain(Permissions.Assistant.Use, Permissions.CompanyScoped);
    }
}
