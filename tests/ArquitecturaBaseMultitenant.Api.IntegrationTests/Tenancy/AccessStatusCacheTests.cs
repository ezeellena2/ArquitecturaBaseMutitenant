using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba la caché de estado de cuenta y membresía y su invalidación explícita. Exige consultar el
/// índice global autorizado al resolver pertenencia.
/// </summary>
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
    public async Task Membership_cache_reads_global_access_index_and_invalidates_one_member()
    {
        var (cache, state) = NewCache();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        state.Accesses = [new UserTenantAccessRow(tenantId, TenantKind.Business, "Empresa A", null,
            TenantStatus.Active, MemberStatus.Active, null)];

        Assert.Equal(MemberStatus.Active,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        state.Accesses = [state.Accesses[0] with { MemberStatus = MemberStatus.Inactive }];
        Assert.Equal(MemberStatus.Active,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        Assert.Equal(1, state.MemberReads);
        Assert.Equal(userId, state.LastUserId);

        await cache.InvalidateMemberAsync(userId, tenantId, Ct);

        Assert.Equal(MemberStatus.Inactive,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        Assert.Equal(2, state.MemberReads);
    }

    [Fact]
    public async Task Membership_cache_rejects_a_tenant_or_kind_absent_from_the_global_index()
    {
        var (cache, state) = NewCache();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        state.Accesses = [new UserTenantAccessRow(tenantId, TenantKind.Business, "Empresa A", null,
            TenantStatus.Active, MemberStatus.Active, null)];

        Assert.Null(await cache.GetMemberStatusAsync(userId, Guid.NewGuid(), TenantKind.Business, Ct));
        Assert.Null(await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Personal, Ct));
    }

    private static (IAccessStatusCache Cache, FakeState State) NewCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton<FakeState>();
        services.AddScoped<IUserStatusReader, FakeUserStatusReader>();
        services.AddScoped<IUserTenantAccessReader, FakeAccessReader>();
        services.AddSingleton<IAccessStatusCache, AccessStatusCache>();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IAccessStatusCache>(), provider.GetRequiredService<FakeState>());
    }

    private sealed class FakeState
    {
        public UserStatus UserStatus { get; set; } = UserStatus.Active;
        public IReadOnlyList<UserTenantAccessRow> Accesses { get; set; } = [];
        public int UserReads { get; set; }
        public int MemberReads { get; set; }
        public Guid? LastUserId { get; set; }
    }

    private sealed class FakeUserStatusReader(FakeState state) : IUserStatusReader
    {
        public Task<UserStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
        {
            state.UserReads++;
            return Task.FromResult<UserStatus?>(state.UserStatus);
        }
    }

    private sealed class FakeAccessReader(FakeState state) : IUserTenantAccessReader
    {
        public Task<IReadOnlyList<UserTenantAccessRow>> ListForUserAsync(Guid userId,
            CancellationToken cancellationToken)
        {
            state.MemberReads++;
            state.LastUserId = userId;
            return Task.FromResult(state.Accesses);
        }
    }
}
