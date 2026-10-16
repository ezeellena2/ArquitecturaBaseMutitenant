using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba prefijos y formatos de claves de caché por alcance. Evita colisiones entre espacios privados,
/// sitios públicos, usuarios y plataforma.
/// </summary>
public sealed class CacheKeyScopeTests
{
    private static readonly Guid TenantA = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid TenantB = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");

    [Fact]
    public void Every_cache_key_has_the_scope_prefix_and_canonical_id()
    {
        Assert.Equal("t:0123456789abcdef0123456789abcdef:roles", CacheKeys.Tenant(TenantA, "roles"));
        Assert.Equal("s:0123456789abcdef0123456789abcdef:page", CacheKeys.PublicSite(TenantA, "page"));
        Assert.Equal("u:0123456789abcdef0123456789abcdef:access", CacheKeys.User(TenantA, "access"));
        Assert.Equal("p:ref:currencies", CacheKeys.Platform("ref:currencies"));
    }

    [Fact]
    public void Tenants_and_public_sites_cannot_share_a_key_for_the_same_resource()
    {
        Assert.NotEqual(CacheKeys.Tenant(TenantA, "roles"), CacheKeys.Tenant(TenantB, "roles"));
        Assert.NotEqual(CacheKeys.PublicSite(TenantA, "page"), CacheKeys.PublicSite(TenantB, "page"));
        Assert.NotEqual(CacheKeys.Tenant(TenantA, "page"), CacheKeys.PublicSite(TenantA, "page"));
    }

    [Fact]
    public void Invalid_scope_ids_and_empty_resources_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => CacheKeys.Tenant(Guid.Empty, "roles"));
        Assert.Throws<ArgumentException>(() => CacheKeys.PublicSite(Guid.Empty, "page"));
        Assert.Throws<ArgumentException>(() => CacheKeys.User(Guid.Empty, "access"));
        Assert.Throws<ArgumentException>(() => CacheKeys.Platform(" "));
        Assert.Throws<ArgumentException>(() => CacheKeys.Tenant(TenantA, " "));
    }

    [Fact]
    public void Caching_registration_provides_HybridCache_without_local_copies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheGenerations();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<HybridCache>());
        // ADR 0052: Redis es la única caché; ninguna instancia guarda una copia que otra no pueda invalidar.
        Assert.Equal(HybridCacheEntryFlags.DisableLocalCache,
            provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value.DefaultEntryOptions?.Flags);
    }

    [Fact]
    public void A_physical_key_carries_the_application_environment_schema_and_generations()
    {
        var entry = new CacheEntry(CacheKeys.Tenant(TenantA, "status"), [CacheTags.Organization(TenantA)],
            TimeSpan.FromSeconds(60));

        Assert.Equal("v1:t:0123456789abcdef0123456789abcdef:status:g7-0", SharedCache.Key(entry, "7-0"));
        Assert.Equal("arquitecturabasemt:Testing:", new CacheOptions { Environment = "Testing" }.KeyPrefix);
        Assert.Equal("t:0123456789abcdef0123456789abcdef:org", CacheTags.Organization(TenantA));
        Assert.Equal("u:0123456789abcdef0123456789abcdef:account", CacheTags.Account(TenantA));
        Assert.Equal("p:settings", CacheTags.PlatformConfiguration);
        Assert.Equal("p:ref", CacheTags.References);
    }
}
