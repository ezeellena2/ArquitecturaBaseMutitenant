using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Settings;

/// <summary>
/// Las cuotas compartidas de E9: doce perfiles cerrados con los nombres del contrato, cotas sin cero, ventanas fijas UTC
/// alineadas para todas las réplicas y una configuración que conserva sus cuotas si no le llegan otras.
/// </summary>
public sealed class PlatformRateLimitsTests
{
    private static readonly Email Sales = Email.Create("ventas@example.com").Value;
    private static readonly Email Support = Email.Create("soporte@example.com").Value;

    [Fact]
    public void The_twelve_profiles_keep_the_contract_names_and_their_partition()
    {
        Assert.Equal(
        [
            "ip", "publicIp", "identity", "tenant", "loginCode", "loginVerify", "invitationPreview", "invitationAccept",
            "assistantQuickQuery", "assistantConversation", "assistantNewKey", "whatsAppWebhook",
        ], Enum.GetValues<RateLimitProfile>().Select(RateLimitProfiles.Name));
        Assert.Equal(RateLimitPartitionKind.Tenant, RateLimitProfiles.PartitionOf(RateLimitProfile.Tenant));
        Assert.All([RateLimitProfile.Identity, RateLimitProfile.AssistantQuickQuery, RateLimitProfile.AssistantConversation,
            RateLimitProfile.AssistantNewKey], profile =>
            Assert.Equal(RateLimitPartitionKind.Identity, RateLimitProfiles.PartitionOf(profile)));
        Assert.All([RateLimitProfile.Ip, RateLimitProfile.PublicIp, RateLimitProfile.LoginCode, RateLimitProfile.LoginVerify,
            RateLimitProfile.InvitationPreview, RateLimitProfile.InvitationAccept, RateLimitProfile.WhatsAppWebhook], profile =>
            Assert.Equal(RateLimitPartitionKind.Ip, RateLimitProfiles.PartitionOf(profile)));
    }

    [Fact]
    public void Initial_limits_are_the_moderate_values_decided_by_the_user()
    {
        var initial = PlatformRateLimits.Initial;

        Assert.True(initial.IsValid());
        Assert.Equal(new RateLimitQuota(300, 60), initial.For(RateLimitProfile.Ip));
        Assert.Equal(new RateLimitQuota(60, 60), initial.For(RateLimitProfile.PublicIp));
        Assert.Equal(new RateLimitQuota(120, 60), initial.For(RateLimitProfile.Identity));
        Assert.Equal(new RateLimitQuota(600, 60), initial.For(RateLimitProfile.Tenant));
        Assert.Equal(new RateLimitQuota(20, 900), initial.For(RateLimitProfile.LoginCode));
        Assert.Equal(new RateLimitQuota(30, 900), initial.For(RateLimitProfile.LoginVerify));
        Assert.Equal(new RateLimitQuota(60, 900), initial.For(RateLimitProfile.InvitationPreview));
        Assert.Equal(new RateLimitQuota(10, 900), initial.For(RateLimitProfile.InvitationAccept));
        Assert.Equal(new RateLimitQuota(20, 60), initial.For(RateLimitProfile.AssistantQuickQuery));
        Assert.Equal(new RateLimitQuota(10, 60), initial.For(RateLimitProfile.AssistantConversation));
        Assert.Equal(new RateLimitQuota(3, 900), initial.For(RateLimitProfile.AssistantNewKey));
        Assert.Equal(new RateLimitQuota(120, 60), initial.For(RateLimitProfile.WhatsAppWebhook));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(1_000_001, 60)]
    [InlineData(10, 0)]
    [InlineData(10, 86_401)]
    public void A_quota_outside_its_bounds_is_rejected_and_the_stored_limits_stay(int permitLimit, int windowSeconds)
    {
        var settings = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Open, 1);
        var invalid = PlatformRateLimits.Initial with { Tenant = new(permitLimit, windowSeconds) };

        Assert.False(invalid.IsValid());
        Assert.Equal(SettingsErrors.InvalidPlatformConfiguration,
            settings.Update(ConsumerSignupMode.Open, BusinessSignupMode.Open, 1, Sales, Support, invalid).Error);
        Assert.Equal(PlatformRateLimits.Initial, settings.RateLimits);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Open, 1, invalid));
    }

    [Fact]
    public void Updating_without_limits_keeps_them_and_with_limits_replaces_the_twelve()
    {
        var custom = PlatformRateLimits.Initial with { LoginCode = new(5, 300) };
        var settings = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Open, 1, custom);

        Assert.True(settings.Update(ConsumerSignupMode.Closed, BusinessSignupMode.Open, 2, Sales, Support).IsSuccess);
        Assert.Equal(custom, settings.RateLimits);
        Assert.True(settings.Update(ConsumerSignupMode.Closed, BusinessSignupMode.Open, 2, Sales, Support,
            PlatformRateLimits.Initial).IsSuccess);
        Assert.Equal(PlatformRateLimits.Initial, settings.RateLimits);
    }

    [Fact]
    public void A_window_starts_at_a_multiple_of_its_length_and_tells_the_whole_seconds_left()
    {
        var nowUtc = new DateTime(2026, 10, 16, 18, 7, 42, 250, DateTimeKind.Utc);

        var minute = RateLimitWindow.Of(nowUtc, new RateLimitQuota(10, 60));
        var quarter = RateLimitWindow.Of(nowUtc, new RateLimitQuota(10, 900));

        Assert.Equal(new DateTime(2026, 10, 16, 18, 7, 0, DateTimeKind.Utc), minute.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 16, 18, 8, 0, DateTimeKind.Utc), minute.EndUtc);
        Assert.Equal(18, minute.SecondsLeft(nowUtc));
        Assert.Equal(new DateTime(2026, 10, 16, 18, 0, 0, DateTimeKind.Utc), quarter.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 16, 18, 15, 0, DateTimeKind.Utc), quarter.EndUtc);
        Assert.Equal(1, minute.SecondsLeft(minute.EndUtc));
        Assert.Throws<ArgumentException>(() => RateLimitWindow.Of(DateTime.SpecifyKind(nowUtc, DateTimeKind.Local),
            new RateLimitQuota(10, 60)));
    }
}
