using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class TenantProvisionerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Repeating_the_same_tenant_provisioning_is_a_no_op()
    {
        var tenantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var userId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var existing = Tenant.CreateBusiness(tenantId, "Organización existente", requiresApproval: false);
        Assert.True(existing.Activate().IsSuccess);
        var tenantRepository = new FakeTenantRepository(existing);
        var memberRepository = new FakeMemberRepository(Member.Invite(userId));
        memberRepository.Member!.Activate(new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(tenantRepository, memberRepository,
            new FakeTenantSettingsRepository(TenantSettings.Create("es-AR", "UTC", "ARS")), tenantId);

        var result = await service.StageAsync(
            Tenant.CreateBusiness(tenantId, "Organización existente", requiresApproval: false),
            TenantSettings.Create("es-AR", "UTC", "ARS"), [userId], userId, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, tenantRepository.AddCount);
        Assert.Equal(0, memberRepository.AddCount);
    }

    [Fact]
    public async Task Reusing_a_tenant_id_for_different_content_is_rejected()
    {
        var tenantId = Guid.Parse("20000000-0000-0000-0000-000000000011");
        var userId = Guid.NewGuid();
        var existing = Tenant.CreateBusiness(tenantId, "Organización original", requiresApproval: false);
        var tenantRepository = new FakeTenantRepository(existing);
        var service = CreateService(tenantRepository, new FakeMemberRepository(null),
            new FakeTenantSettingsRepository(null), tenantId);

        var result = await service.StageAsync(
            Tenant.CreateBusiness(tenantId, "Otra organización", requiresApproval: false),
            TenantSettings.Create("es-AR", "UTC", "ARS"), [userId], userId, Ct);

        Assert.Equal(TenantErrors.ProvisioningConflictCode, result.Error.Code);
        Assert.Equal(0, tenantRepository.AddCount);
    }

    [Fact]
    public async Task Explicit_owner_entry_rejects_personal_wrong_scope_and_removed_members()
    {
        var tenant = Tenant.CreateBusiness("Alta explícita", false);
        Assert.True(tenant.Activate().IsSuccess);
        var tenants = new FakeTenantRepository(null);
        var settings = TenantSettings.Create("es-AR", "UTC", "ARS");
        var owner = Member.Invite();
        var wrongScope = CreateService(tenants, new FakeMemberRepository(null), new FakeTenantSettingsRepository(null), Guid.NewGuid());
        Assert.True((await wrongScope.StageBusinessOwnerAsync(tenant, settings, owner, Ct)).IsFailure);
        var correct = CreateService(tenants, new FakeMemberRepository(null), new FakeTenantSettingsRepository(null), tenant.Id);
        Assert.True((await correct.StageBusinessOwnerAsync(Tenant.CreatePersonal("Personal"), settings, owner, Ct)).IsFailure);
        Assert.True((await correct.StageBusinessOwnerAsync(tenant, settings, owner, Ct, Guid.Empty)).IsFailure);
        Assert.True(owner.Remove().IsSuccess);
        Assert.True((await correct.StageBusinessOwnerAsync(tenant, settings, owner, Ct)).IsFailure);
        Assert.Equal(0, tenants.AddCount);
    }

    private static TenantProvisioner CreateService(FakeTenantRepository tenants,
        FakeMemberRepository members, FakeTenantSettingsRepository settings, Guid tenantId)
    {
        var tenantContext = new FakeTenantContext { TenantId = tenantId, TenantKind = TenantKind.Business };
        var replayVerifier = new TenantProvisioningReplayVerifier(tenants, members, null!, null!, null!, settings);
        var newTenantProvisioner = new TenantSpaceProvisioner(tenants, members, null!, null!, null!, settings,
            TimeProvider.System);
        return new TenantProvisioner(newTenantProvisioner, replayVerifier, tenantContext);
    }

    private sealed class FakeTenantRepository(Tenant? existing) : ITenantRepository
    {
        public int AddCount { get; private set; }

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(existing?.Id == id ? existing : null);

        public void Add(Tenant tenant) => AddCount++;
    }

    private sealed class FakeMemberRepository(Member? member) : IMemberRepository
    {
        public Member? Member { get; } = member;
        public int AddCount { get; private set; }

        public Task LockUserAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Member?.UserId == userId ? Member : null);
          public Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
              Task.FromResult(Member?.Id == id ? Member : null);
          public Task<Member?> GetByIdAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken) =>
              Task.FromResult(Member?.Id == id ? Member : null);
          public void Add(Member value) => AddCount++;
          public void MarkUpdated(Member value) { }
    }

    private sealed class FakeTenantSettingsRepository(TenantSettings? current) : ITenantSettingsRepository
    {
        public Task<TenantSettings?> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(current);
        public Task<TenantSettings?> GetCurrentAsync(uint expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult(current);
        public void Add(TenantSettings settings) { }
        public void MarkUpdated(TenantSettings settings) { }
    }
}
