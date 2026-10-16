using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// La caché compartida de estado de cuenta y membresía (ADR 0052): la lectura se repite desde Redis hasta que avanza la
/// generación de su etiqueta, como lo hace el commit que cambia el estado. Exige consultar el índice global autorizado al
/// resolver pertenencia, y Personal y Business nunca comparten una entrada.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccessStatusCacheTests : IDisposable
{
    private readonly FakeState state = new();
    private readonly WebApplicationFactory<Program> host;

    public AccessStatusCacheTests(ApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(state);
            services.Replace(ServiceDescriptor.Scoped<IUserStatusReader, FakeUserStatusReader>());
            services.Replace(ServiceDescriptor.Scoped<IUserTenantAccessReader, FakeAccessReader>());
        }));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => host.Dispose();

    [Fact]
    public async Task User_status_is_cached_until_its_generation_advances()
    {
        var cache = host.Services.GetRequiredService<IAccessStatusCache>();
        var userId = Guid.NewGuid();

        Assert.Equal(UserStatus.Active, await cache.GetUserStatusAsync(userId, Ct));
        state.UserStatus = UserStatus.Suspended;
        Assert.Equal(UserStatus.Active, await cache.GetUserStatusAsync(userId, Ct));
        Assert.Equal(1, state.UserReads);

        await CacheGenerations.AdvanceAsync(host.Services, Ct, CacheTags.Account(userId));

        Assert.Equal(UserStatus.Suspended, await cache.GetUserStatusAsync(userId, Ct));
        Assert.Equal(2, state.UserReads);
    }

    [Fact]
    public async Task Membership_cache_reads_global_access_index_until_the_members_generation_advances()
    {
        var cache = host.Services.GetRequiredService<IAccessStatusCache>();
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

        await CacheGenerations.AdvanceAsync(host.Services, Ct, CacheTags.Members(tenantId));

        Assert.Equal(MemberStatus.Inactive,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        Assert.Equal(2, state.MemberReads);
    }

    [Fact]
    public async Task Membership_cache_rejects_a_tenant_or_kind_absent_from_the_global_index()
    {
        var cache = host.Services.GetRequiredService<IAccessStatusCache>();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        state.Accesses = [new UserTenantAccessRow(tenantId, TenantKind.Business, "Empresa A", null,
            TenantStatus.Active, MemberStatus.Active, null)];

        Assert.Null(await cache.GetMemberStatusAsync(userId, Guid.NewGuid(), TenantKind.Business, Ct));
        // La clase va en la clave: la entrada Business recién leída no responde por Personal.
        Assert.Equal(MemberStatus.Active,
            await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Business, Ct));
        Assert.Null(await cache.GetMemberStatusAsync(userId, tenantId, TenantKind.Personal, Ct));
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

        public Task<IReadOnlyCollection<Guid>> ListActiveAsync(IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Guid>>(
            state.UserStatus == UserStatus.Active ? userIds : []);

        public Task<Result<IReadOnlyList<UserDeletionStateRow>>> ListDeletionStatesAsync(IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<UserDeletionStateRow>>> LockDeletionStatesAsync(IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) => throw new NotSupportedException();
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
