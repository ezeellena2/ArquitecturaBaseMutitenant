using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Roles;

/// <summary>Los permisos leídos de una vez (base de la 12) deciden solo en la organización donde se leyeron.</summary>
public sealed class PermissionServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Permissions_read_in_one_organization_never_decide_in_another()
    {
        var tenant = new FakeTenantContext { TenantId = Guid.NewGuid(), TenantKind = TenantKind.Business };
        var otherCompany = Guid.NewGuid();
        var service = new PermissionService(new CountingPermissionCache([Permissions.Company.Members.Read],
                new Dictionary<Guid, IReadOnlyList<string>>()), tenant, new CompanyActivityReader(otherCompany),
            new FakeTimeProvider(), NullLogger<PermissionService>.Instance);
        var granted = await service.GetBusinessPermissionsAsync(Guid.NewGuid(), Ct);

        Assert.Equal(tenant.TenantId, granted.TenantId);
        Assert.True(await service.HasCompanyPermissionAsync(granted, otherCompany, Permissions.Company.Members.Read, Ct));
        tenant.TenantId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.HasCompanyPermissionAsync(granted, otherCompany, Permissions.Company.Members.Read, Ct));
    }
}
