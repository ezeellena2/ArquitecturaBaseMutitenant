using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;
public sealed class WhatsAppCodeQuotaTests
{
    [Fact]
    public async Task Waiting_for_lock_across_midnight_reserves_the_new_day_even_when_yesterday_is_full()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 23, 59, 58, TimeSpan.Zero));
        var repository = new Quotas(); var settings = new Settings();
        var old = WhatsAppCodeQuota.Create(settings.PhoneNumberId!, new DateOnly(2026, 10, 8));
        Assert.True(old.Reserve(1, clock.GetUtcNow().UtcDateTime).IsSuccess);
        repository.Add(old);
        repository.OnLock = () => clock.Advance(TimeSpan.FromSeconds(2));
        var issuer = new WhatsAppCodeQuotaIssuer(repository, settings, clock);
        var destination = LoginCodeDestination.ForPhone(PhoneNumber.Create("+5491134567890").Value, "whatsapp");
        Assert.True((await issuer.ReserveAsync(destination, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(2, repository.Rows.Count);
        Assert.Equal(1, repository.Rows[(settings.PhoneNumberId!, new DateOnly(2026, 10, 9))].ReservedCount);
    }
    [Fact]
    public async Task Quota_uses_UTC_midnight_for_renewal_and_new_day_and_disabled_channel_never_reserves()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 23, 59, 58, TimeSpan.Zero));
        var repository = new Quotas(); var settings = new Settings();
        var issuer = new WhatsAppCodeQuotaIssuer(repository, settings, clock);
        var destination = LoginCodeDestination.ForPhone(PhoneNumber.Create("+5491134567890").Value, "whatsapp");
        Assert.True((await issuer.ReserveAsync(destination, TestContext.Current.CancellationToken)).IsSuccess);
        var denied = await issuer.ReserveAsync(destination, TestContext.Current.CancellationToken);
        Assert.Equal(WhatsAppCodeErrors.DailyLimitCode, denied.Error.Code);
        Assert.Equal(2, denied.Error.Metadata!["retryAfter"]);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True((await issuer.ReserveAsync(destination, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(2, repository.Rows.Count);
        Assert.All(repository.Rows.Values, value => Assert.Equal(1, value.ReservedCount));
        settings.IsEnabled = false;
        Assert.Equal(LoginChannelErrors.Unavailable.Code, (await issuer.ReserveAsync(destination, TestContext.Current.CancellationToken)).Error.Code);
        Assert.Equal(2, repository.Rows.Count);
    }
    internal sealed class Settings : IWhatsAppChannelSettings
    {
        public bool IsEnabled { get; set; } = true;
        public string? PhoneNumberId { get; set; } = "quota-fixture";
        public string? ContactPhone => null;
        public IReadOnlyList<string> Countries => ["AR"];
        public int DailyAuthCodeLimit { get; set; } = 1;
        public Result ValidateNewDestination(LoginCodeDestination destination) => Result.Success();
    }
    private sealed class Quotas : IWhatsAppCodeQuotaRepository
    {
        public Action? OnLock { get; set; }
        public Dictionary<(string, DateOnly), WhatsAppCodeQuota> Rows { get; } = [];
        public Task LockAsync(string id, DateOnly day, CancellationToken ct) { OnLock?.Invoke(); OnLock = null; return Task.CompletedTask; }
        public Task<WhatsAppCodeQuota?> GetAsync(string id, DateOnly day, CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault((id, day)));
        public void Add(WhatsAppCodeQuota value) => Rows.Add((value.PhoneNumberId, value.DayUtc), value);
    }
}
