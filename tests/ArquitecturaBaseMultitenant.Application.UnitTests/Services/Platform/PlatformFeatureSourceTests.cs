using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Validation.Platform;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Platform;

/// <summary>El contrato de Plataforma conserva sus tres orígenes aunque el dominio sume otros.</summary>
public sealed class PlatformFeatureSourceTests
{
    [Fact]
    public void Platform_contract_keeps_only_plan_manual_and_trial() =>
        Assert.Equal(["Plan", "Manual", "Trial"], Enum.GetNames<PlatformFeatureSource>());

    [Theory]
    [InlineData(PlatformFeatureSource.Plan, TenantFeatureSource.Plan)]
    [InlineData(PlatformFeatureSource.Manual, TenantFeatureSource.Manual)]
    [InlineData(PlatformFeatureSource.Trial, TenantFeatureSource.Trial)]
    public void The_change_and_the_list_translate_each_source_to_its_domain_twin(
        PlatformFeatureSource platform, TenantFeatureSource domain)
    {
        Assert.Equal(domain, Request(platform, null).DomainSource);
        Assert.Equal(platform, PlatformFeatureResponse.From("test-feature", true, domain, null, 1).Source);
    }

    [Fact]
    public void A_change_without_source_has_no_domain_source() =>
        Assert.Null(Request(null, null).DomainSource);

    [Theory]
    [InlineData(TenantFeatureSource.Approved)]
    [InlineData(TenantFeatureSource.Subscription)]
    public void A_source_outside_platform_never_reaches_the_platform_contract(TenantFeatureSource source) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformFeatureResponse.From("test-feature", true, source, null, 1));

    [Fact]
    public void The_validator_keeps_its_rules_over_the_platform_source()
    {
        var validator = new UpdatePlatformFeatureRequestValidator();
        Assert.True(validator.Validate(Request(PlatformFeatureSource.Manual, null)).IsValid);
        Assert.True(validator.Validate(Request(PlatformFeatureSource.Trial, new DateOnly(2026, 12, 31))).IsValid);
        Assert.False(validator.Validate(Request(PlatformFeatureSource.Trial, null)).IsValid);
        Assert.False(validator.Validate(Request((PlatformFeatureSource)3, null)).IsValid);
        Assert.False(validator.Validate(Request(null, null)).IsValid);
    }

    private static UpdatePlatformFeatureRequest Request(PlatformFeatureSource? source, DateOnly? expiresOn) =>
        new(true, source, expiresOn, 0, "Motivo de prueba");
}
