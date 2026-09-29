using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

public sealed class AccessStatusCacheTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task User_status_is_cached_until_explicit_invalidation()
    {
        var (cache, state) = NewCache();
        var userId = Guid.NewGuid();

        Assert.Equal(UserStatus.Active, await cache.GetUserStatusAsync(userId, Ct));
        state.UserStatus = UserStatus.Suspended;
        Assert.Equal(UserStatus.Active, await cache.GetUserStatusAsync(userId, Ct));
        Assert.Equal(1, state.UserReads);

        await cache.InvalidateUserAsync(userId, Ct);

        Assert.Equal(UserStatus.Suspended, await cache.GetUserStatusAsync(userId, Ct));
        Assert.Equal(2, state.UserReads);
    }

    [Fact]
    public async Task Membership_cache_uses_tenant_scope_and_invalidates_one_member()
    {
        var (cache, state) = NewCache();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        Assert.Equal(MemberStatus.Active,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        state.MemberStatus = MemberStatus.Inactive;
        Assert.Equal(MemberStatus.Active,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        Assert.Equal(1, state.MemberReads);
        Assert.Equal(tenantId, state.LastTenantId);

        await cache.InvalidateMemberAsync(userId, tenantId, Ct);

        Assert.Equal(MemberStatus.Inactive,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        Assert.Equal(2, state.MemberReads);
    }

    private static (IAccessStatusCache Cache, FakeState State) NewCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton<FakeState>();
        services.AddScoped<FakeInitializer>();
        services.AddScoped<ITenantAccessInitializer>(provider => provider.GetRequiredService<FakeInitializer>());
        services.AddScoped<IUserStatusReader, FakeUserStatusReader>();
        services.AddScoped<IMemberReader, FakeMemberReader>();
        services.AddSingleton<IAccessStatusCache, AccessStatusCache>();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IAccessStatusCache>(), provider.GetRequiredService<FakeState>());
    }

    private sealed class FakeState
    {
        public UserStatus UserStatus { get; set; } = UserStatus.Active;
        public MemberStatus MemberStatus { get; set; } = MemberStatus.Active;
        public int UserReads { get; set; }
        public int MemberReads { get; set; }
        public Guid? LastTenantId { get; set; }
    }

    private sealed class FakeInitializer : ITenantAccessInitializer
    {
        public Guid? TenantId { get; private set; }
        public void SetFromAccess(Guid tenantId, TenantKind kind) => TenantId = tenantId;
    }

    private sealed class FakeUserStatusReader(FakeState state) : IUserStatusReader
    {
        public Task<UserStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
        {
            state.UserReads++;
            return Task.FromResult<UserStatus?>(state.UserStatus);
        }
    }

    private sealed class FakeMemberReader(FakeState state, FakeInitializer initializer) : IMemberReader
    {
        public Task<MemberRow?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            state.MemberReads++;
            state.LastTenantId = initializer.TenantId;
            return Task.FromResult<MemberRow?>(new MemberRow(initializer.TenantId!.Value, userId,
                state.MemberStatus, state.UserStatus, null));
        }

        public Task<IReadOnlyList<MemberRow>> ListCurrentTenantAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MemberRow>>([]);
    }
}
