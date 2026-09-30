using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Cachea estado de identidad y membresía desde el índice global de accesos.</summary>
internal sealed class AccessStatusCache(HybridCache cache, IServiceScopeFactory scopes) : IAccessStatusCache
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60),
    };

    public async Task<UserStatus?> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateInOwnScopeAsync<IUserStatusReader, Guid, UserStatus?>(
            UserKey(userId), scopes, userId,
            static (reader, id, ct) => reader.GetStatusAsync(id, ct), CacheOptions, cancellationToken);

    public async Task<MemberStatus?> GetMemberStatusAsync(Guid userId, Guid tenantId, TenantKind kind,
        CancellationToken cancellationToken)
    {
        if (kind != TenantKind.Personal && kind != TenantKind.Business)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "An access member requires a tenant kind.");
        }

        return await cache.GetOrCreateInOwnScopeAsync(MemberKey(userId, tenantId),
            scopes, (userId, tenantId, kind),
            static async (services, state, ct) =>
            {
                var accesses = await services.GetRequiredService<IUserTenantAccessReader>()
                    .ListForUserAsync(state.userId, ct);
                return accesses.FirstOrDefault(access => access.TenantId == state.tenantId
                    && access.Kind == state.kind)?.MemberStatus;
            }, CacheOptions, cancellationToken: cancellationToken);
    }

    public ValueTask InvalidateUserAsync(Guid userId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(UserKey(userId), cancellationToken);

    public ValueTask InvalidateMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(MemberKey(userId, tenantId), cancellationToken);

    private static string UserKey(Guid userId) => CacheKeys.User(userId, "status");

    private static string MemberKey(Guid userId, Guid tenantId) =>
        CacheKeys.Tenant(tenantId, "member:" + userId.ToString("N", System.Globalization.CultureInfo.InvariantCulture) + ":status");
}
