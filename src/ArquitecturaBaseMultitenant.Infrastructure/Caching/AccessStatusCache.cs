using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Estado de identidad y de membresía en la caché compartida (ADR 0052), 60 s. La membresía se lee del índice global
/// de accesos y lleva su clase en la clave: Personal y Business nunca comparten una entrada.
/// </summary>
internal sealed class AccessStatusCache(SharedCache cache) : IAccessStatusCache
{
    private static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    public Task<UserStatus?> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken) =>
        cache.GetOrLoadAsync(
            new CacheEntry(CacheKeys.User(userId, "status"), [CacheTags.Account(userId)], Expiration),
            userId,
            static (services, id, ct) => services.GetRequiredService<IUserStatusReader>().GetStatusAsync(id, ct),
            cancellationToken);

    public Task<MemberStatus?> GetMemberStatusAsync(Guid userId, Guid tenantId, TenantKind kind,
        CancellationToken cancellationToken)
    {
        var scope = kind switch
        {
            TenantKind.Personal => "personal",
            TenantKind.Business => "business",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "An access member requires a tenant kind."),
        };
        var key = CacheKeys.Tenant(tenantId,
            "member:" + userId.ToString("N", CultureInfo.InvariantCulture) + ":" + scope + ":status");
        return cache.GetOrLoadAsync(
            new CacheEntry(key,
                [CacheTags.Organization(tenantId), CacheTags.Account(userId), CacheTags.Members(tenantId)], Expiration),
            (UserId: userId, TenantId: tenantId, Kind: kind),
            static async (services, state, ct) =>
            {
                var accesses = await services.GetRequiredService<IUserTenantAccessReader>()
                    .ListForUserAsync(state.UserId, ct);
                return accesses.FirstOrDefault(access => access.TenantId == state.TenantId
                    && access.Kind == state.Kind)?.MemberStatus;
            },
            cancellationToken);
    }
}
