using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Features;

public sealed class TenantFeatureTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Trial_is_disabled_at_the_exact_expiry_boundary()
    {
        var feature = TenantFeature.Create(Guid.NewGuid(), "test-feature", true, TenantFeatureSource.Trial, NowUtc).Value;
        Assert.True(feature.IsEnabledAt(NowUtc.AddTicks(-1)));
        Assert.False(feature.IsEnabledAt(NowUtc));
        Assert.False(feature.IsEnabledAt(NowUtc.AddTicks(1)));
    }

    [Fact]
    public void Invalid_updates_do_not_change_the_existing_grant()
    {
        var feature = TenantFeature.Create(Guid.NewGuid(), "test-feature", true, TenantFeatureSource.Plan, null).Value;
        Assert.True(feature.Update(false, TenantFeatureSource.Trial, null).IsFailure);
        Assert.True(feature.Update(false, (TenantFeatureSource)999, null).IsFailure);
        Assert.True(feature.Enabled);
        Assert.Equal(TenantFeatureSource.Plan, feature.Source);
        Assert.Null(feature.ExpiresAtUtc);
        Assert.True(feature.IsEnabledAt(NowUtc));
    }

    [Fact]
    public void Manual_disable_and_non_utc_instants_are_respected()
    {
        var feature = TenantFeature.Create(Guid.NewGuid(), "test-feature", true, TenantFeatureSource.Manual, null).Value;
        Assert.True(feature.Update(false, TenantFeatureSource.Manual, null).IsSuccess);
        Assert.False(feature.IsEnabledAt(NowUtc));
        Assert.Throws<ArgumentException>(() => feature.Update(true, TenantFeatureSource.Trial, DateTime.SpecifyKind(NowUtc, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Definitions_keep_consumer_and_business_scopes_separate_and_the_template_catalog_empty()
    {
        var business = new FeatureDefinition("test-business", FeatureScope.Business, DefaultEnabled: false);
        Assert.True(business.Supports(TenantKind.Business));
        Assert.False(business.Supports(TenantKind.Personal));
        var both = new FeatureDefinition("test-shared", FeatureScope.Both, DefaultEnabled: false);
        Assert.True(both.Supports(TenantKind.Business));
        Assert.True(both.Supports(TenantKind.Personal));
        Assert.Empty(Domain.Features.Features.All);
    }
}
