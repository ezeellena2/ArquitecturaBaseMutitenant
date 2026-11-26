using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Features;

public sealed class TenantFeatureSourceExtensionsTests
{
    [Theory]
    [InlineData(TenantFeatureSource.Plan, true)]
    [InlineData(TenantFeatureSource.Manual, true)]
    [InlineData(TenantFeatureSource.Trial, true)]
    [InlineData(TenantFeatureSource.Approved, false)]
    [InlineData(TenantFeatureSource.Subscription, false)]
    public void Only_plan_manual_and_trial_are_managed_by_platform(TenantFeatureSource source, bool managed) =>
        Assert.Equal(managed, source.IsPlatformManaged());

    [Fact]
    public void The_new_sources_go_after_the_saved_ones() =>
        Assert.Equal(["Plan", "Manual", "Trial", "Approved", "Subscription"], Enum.GetNames<TenantFeatureSource>());
}
