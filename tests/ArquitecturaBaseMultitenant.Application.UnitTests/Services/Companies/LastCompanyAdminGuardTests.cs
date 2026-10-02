using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Companies;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Companies;

public sealed class LastCompanyAdminGuardTests
{
    [Fact]
    public async Task Cannot_remove_the_only_active_company_admin()
    {
        var reader = new FakeCompanyAdminReader { Count = 1 };
        var guard = new LastCompanyAdminGuard(reader, new FakeCompanyAdminLock(),
            new FakeTenantContext(TenantKind.Business));

        var result = await guard.CheckAnotherActiveAdminAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal(CompanyMembershipErrors.LastCompanyAdmin, result.Error);
    }

    [Fact]
    public async Task Allows_removal_when_another_active_company_admin_exists()
    {
        var reader = new FakeCompanyAdminReader { Count = 2 };
        var guard = new LastCompanyAdminGuard(reader, new FakeCompanyAdminLock(),
            new FakeTenantContext(TenantKind.Business));

        var result = await guard.CheckAnotherActiveAdminAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Serializes_changes_with_a_lock_for_the_company()
    {
        var companyId = Guid.CreateVersion7();
        var locks = new FakeCompanyAdminLock();
        var guard = new LastCompanyAdminGuard(new FakeCompanyAdminReader(), locks,
            new FakeTenantContext(TenantKind.Business));

        await guard.LockAsync(companyId, TestContext.Current.CancellationToken);

        Assert.Equal(companyId, locks.CompanyId);
    }

    [Fact]
    public async Task Lock_requires_a_business_tenant()
    {
        var guard = new LastCompanyAdminGuard(new FakeCompanyAdminReader(), new FakeCompanyAdminLock(),
            new FakeTenantContext(TenantKind.Personal));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            guard.LockAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken));
    }

    private sealed class FakeCompanyAdminReader : ICompanyAdminReader
    {
        public int Count { get; init; }

        public Task<int> CountActiveCompanyAdminsAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Count);
    }

    private sealed class FakeCompanyAdminLock : ICompanyAdminLock
    {
        public Guid? CompanyId { get; private set; }

        public Task LockAssignmentsAsync(Guid companyId, CancellationToken cancellationToken)
        {
            CompanyId = companyId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantContext(TenantKind tenantKind) : ITenantContext
    {
        public Guid? TenantId { get; } = Guid.CreateVersion7();
        public TenantKind? TenantKind { get; } = tenantKind;
        public Guid RequiredTenantId => TenantId!.Value;
    }
}
