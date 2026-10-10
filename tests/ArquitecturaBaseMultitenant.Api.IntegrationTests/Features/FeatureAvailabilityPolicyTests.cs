using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Features;

public sealed class FeatureAvailabilityPolicyTests
{
    [Theory]
    [InlineData(Access.Consumer, TenantKind.Personal)]
    [InlineData(Access.Business, TenantKind.Business)]
    [InlineData(Access.Platform, null)]
    public async Task Explicit_policy_can_decide_its_own_key_for_each_compatible_access(Access access, TenantKind? kind)
    {
        var manager = new Manager(false);
        var service = Service(access, kind, manager, [new Policy("own", true)]);
        Assert.True(await service.IsEnabledAsync("own", TestContext.Current.CancellationToken));
        Assert.False(await service.IsEnabledAsync("other", TestContext.Current.CancellationToken));
        Assert.False(await service.IsEnabledAsync("unknown", TestContext.Current.CancellationToken));
        Assert.Equal(access == Access.Platform ? 0 : 1, manager.Calls);
    }
    [Fact]
    public async Task Null_policy_delegates_to_tenant_features_and_platform_requires_an_explicit_decision()
    {
        var manager = new Manager(true);
        Assert.True(await Service(Access.Business, TenantKind.Business, manager, [new Policy("own", null)]).IsEnabledAsync("own", TestContext.Current.CancellationToken));
        Assert.Equal(1, manager.Calls);
        Assert.False(await Service(Access.Platform, null, manager, [new Policy("own", null)]).IsEnabledAsync("own", TestContext.Current.CancellationToken));
        Assert.False(await Service(Access.Platform, null, manager, []).IsEnabledAsync("own", TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Emergency_and_incompatible_scope_precede_policy_and_duplicate_keys_are_rejected()
    {
        var manager = new Manager(true);
        Assert.False(await Service(Access.Consumer, null, manager, [new Policy("own", true)]).IsEnabledAsync("own", TestContext.Current.CancellationToken));
        Assert.False(await Service(Access.Business, TenantKind.Personal, manager, [new Policy("own", true)], businessOnly: true).IsEnabledAsync("own", TestContext.Current.CancellationToken));
        Assert.False(await Service(Access.Platform, null, manager, [new Policy("own", true)], emergency: true).IsEnabledAsync("own", TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => Service(Access.Platform, null, manager, [new Policy("own", true), new Policy("own", false)]));
        Assert.Equal(0, manager.Calls);
    }
    private static FeatureService Service(Access access, TenantKind? kind, Manager manager,
        IReadOnlyList<IFeatureAvailabilityPolicy> policies, bool emergency = false, bool businessOnly = false) =>
        new(manager, new Catalog(businessOnly), new Tenant(kind), new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FeatureManagement:own"] = emergency ? "false" : null }).Build(),
            TimeProvider.System, NullLogger<FeatureService>.Instance, new User(access), policies);
    private sealed record User(Access? Access) : ICurrentUser { public Guid? UserId => Guid.Empty; }
    private sealed record Tenant(TenantKind? TenantKind) : ITenantContext { public Guid? TenantId => TenantKind is null ? null : Guid.Empty; public Guid RequiredTenantId => TenantId!.Value; }
    private sealed class Catalog(bool businessOnly) : IFeatureCatalog
    {
        public IReadOnlyList<ArquitecturaBaseMultitenant.Domain.Features.FeatureDefinition> All { get; } = [new("own", businessOnly ? FeatureScope.Business : FeatureScope.Both, false), new("other", FeatureScope.Both, false)];
    }
    private sealed record Policy(string Key, bool? Decision) : IFeatureAvailabilityPolicy
    {
        public Task<bool?> IsAvailableAsync(Access access, CancellationToken cancellationToken) => Task.FromResult(Decision);
    }
    private sealed class Manager(bool enabled) : IFeatureManager
    {
        public int Calls { get; private set; }
        public async IAsyncEnumerable<string> GetFeatureNamesAsync() { await Task.CompletedTask; yield return "own"; }
        public Task<bool> IsEnabledAsync(string feature) { Calls++; return Task.FromResult(enabled); }
        public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context) => IsEnabledAsync(feature);
    }
}
