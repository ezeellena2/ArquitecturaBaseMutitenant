using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;
public sealed class WhatsAppCodeQuotaTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Owner = Guid.NewGuid();
    private static LoginCodeDestination Destination => LoginCodeDestination.ForPhone(PhoneNumber.Create("+5491134567890").Value, "whatsapp");

    [Fact]
    public async Task New_capacity_stops_at_80_while_verified_login_can_use_remaining_total_100()
    {
        var f = new Fixture();
        for (var i = 0; i < 80; i++) { f.Request.IpAddress = "192.0.2." + (i / 20 + 1); Assert.True((await f.Reserve()).IsSuccess); }
        f.Request.IpAddress = "192.0.2.99";
        Assert.Equal(WhatsAppCodeErrors.DailyLimitCode, (await f.Reserve(LoginCodePurpose.BusinessSignup, Owner)).Error.Code);
        for (var i = 0; i < 20; i++) Assert.True((await f.Reserve(LoginCodePurpose.Login, Owner)).IsSuccess);
        Assert.Equal(WhatsAppCodeErrors.DailyLimitCode, (await f.Reserve(LoginCodePurpose.Login, Owner)).Error.Code);
        var row = Assert.Single(f.Quotas.Rows.Values);
        Assert.Equal(100, row.ReservedCount); Assert.Equal(80, row.NewReservedCount);
        Assert.Equal(4, f.Quotas.Ips.Count);
    }
    [Fact]
    public async Task Protected_capacity_is_a_floor_not_a_ceiling_for_verified_logins()
    {
        var f = new Fixture();
        for (var i = 0; i < 100; i++) Assert.True((await f.Reserve(LoginCodePurpose.Login, Owner)).IsSuccess);
        Assert.True((await f.Reserve(LoginCodePurpose.Login, Owner)).IsFailure);
        Assert.Equal(100, Assert.Single(f.Quotas.Rows.Values).ReservedCount);
        Assert.Equal(0, Assert.Single(f.Quotas.Rows.Values).NewReservedCount); Assert.Empty(f.Quotas.Ips);
    }
    [Fact]
    public async Task One_IP_has_20_new_sends_across_purposes_and_unknown_IP_is_a_stable_bucket()
    {
        var f = new Fixture();
        for (var i = 0; i < 20; i++) Assert.True((await f.Reserve(i % 2 == 0 ? LoginCodePurpose.Signup : LoginCodePurpose.AccountRecovery)).IsSuccess);
        Assert.True((await f.Reserve(LoginCodePurpose.VerifyDestination, Owner)).IsFailure);
        Assert.Equal(20, Assert.Single(f.Quotas.Rows.Values).ReservedCount);
        Assert.Equal(20, Assert.Single(f.Quotas.Ips.Values).ReservedCount);
        f.Request.IpAddress = "192.0.2.1";
        Assert.True((await f.Reserve()).IsSuccess);
    }
    [Theory]
    [InlineData(LoginCodePurpose.BusinessSignup, true, true)]
    [InlineData(LoginCodePurpose.VerifyDestination, true, true)]
    [InlineData(LoginCodePurpose.Login, false, true)]
    [InlineData(LoginCodePurpose.Login, true, false)]
    public async Task Owner_present_does_not_protect_non_login_pending_or_unusable_phone(LoginCodePurpose purpose, bool verified, bool active)
    {
        var f = new Fixture(); f.Settings.DailyAuthCodeLimit = 1; f.Methods.Verified = verified; f.Memberships.Active = active;
        if (!active) f.Methods.Manager = Guid.NewGuid();
        var denied = await f.Reserve(purpose, Owner);
        Assert.Equal(WhatsAppCodeErrors.DailyLimitCode, denied.Error.Code);
        Assert.Empty(f.Quotas.Rows); Assert.Empty(f.Quotas.Ips);
    }
    [Theory]
    [InlineData(1, 0)] [InlineData(2, 1)] [InlineData(6, 4)] [InlineData(99, 79)]
    public async Task Tiny_and_fractional_caps_round_protection_up_without_exceeding_total(int total, int newLimit)
    {
        var f = new Fixture(); f.Settings.DailyAuthCodeLimit = total; f.Settings.DailyNewAuthCodeIpLimit = 10000;
        Assert.Equal(newLimit, ((IWhatsAppChannelSettings)f.Settings).DailyNewAuthCodeLimit);
        for (var i = 0; i < newLimit; i++) Assert.True((await f.Reserve()).IsSuccess);
        Assert.True((await f.Reserve()).IsFailure);
        for (var i = newLimit; i < total; i++) Assert.True((await f.Reserve(LoginCodePurpose.Login, Owner)).IsSuccess);
        Assert.True((await f.Reserve(LoginCodePurpose.Login, Owner)).IsFailure);
        Assert.Equal(total, Assert.Single(f.Quotas.Rows.Values).ReservedCount);
    }
    [Fact]
    public async Task Waiting_for_lock_across_midnight_uses_fresh_UTC_day_and_retry_after()
    {
        var f = new Fixture(); f.Settings.DailyAuthCodeLimit = 1;
        Assert.True((await f.Reserve(LoginCodePurpose.Login, Owner)).IsSuccess);
        var denied = await f.Reserve(LoginCodePurpose.Login, Owner);
        Assert.Equal(2, denied.Error.Metadata!["retryAfter"]);
        f.Quotas.OnLock = () => f.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True((await f.Reserve(LoginCodePurpose.Login, Owner)).IsSuccess);
        Assert.Equal(2, f.Quotas.Rows.Count);
        Assert.All(f.Quotas.Rows.Values, row => Assert.Equal(1, row.ReservedCount));
        f.Settings.IsEnabled = false;
        Assert.Equal(LoginChannelErrors.Unavailable.Code, (await f.Reserve()).Error.Code);
    }
    private static FixtureHasher Hasher() => new();
    private sealed class FixtureHasher : IWhatsAppQuotaIpHasher
    {
        public string Hash(string? ip, string number, DateOnly day) => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ip ?? "unknown")));
    }
    private sealed class Fixture
    {
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 8, 23, 59, 58, TimeSpan.Zero));
        public Quotas Quotas { get; } = new(); public Settings Settings { get; } = new();
        public Methods Methods { get; } = new(); public Memberships Memberships { get; } = new(); public Request Request { get; } = new();
        public Task<Result> Reserve(LoginCodePurpose purpose = LoginCodePurpose.Signup, Guid? owner = null)
        {
            var availability = new LoginMethodAvailabilityPolicy([new Channel()], new Google(), Memberships);
            return new WhatsAppCodeQuotaIssuer(Quotas, Settings, Clock, Methods, availability, Request, Hasher()).ReserveAsync(Destination, purpose, owner, Ct);
        }
    }
    internal sealed class Settings : IWhatsAppChannelSettings
    {
        public string AppName => "Fixture"; public bool IsEnabled { get; set; } = true;
        public string PhoneNumberId => "quota-fixture"; public string? ContactPhone => null;
        public IReadOnlyList<string> Countries => []; public int DailyAuthCodeLimit { get; set; } = 100;
        public int DailyNewAuthCodeIpLimit { get; set; } = 20;
        public Result ValidateNewDestination(LoginCodeDestination destination) => Result.Success();
    }
    private sealed class Request : IRequestInfo { public string? IpAddress { get; set; } public string? UserAgent => null; }
    private sealed class Google : IGoogleAvailability { public bool IsEnabled => false; }
    private sealed class Memberships : ILoginMethodMembershipReader
    { public bool Active { get; set; } = true; public Task<bool> IsActiveAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult(Active); }
    private sealed class Methods : ILoginMethodReader
    {
        public bool Verified { get; set; } = true; public Guid? Manager { get; set; }
        public Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<LoginMethodRow>>(
            id == Owner ? [new(Guid.NewGuid(), LoginMethodType.Phone, Destination.Value, true, Verified ? new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) : null, Manager)] : []);
    }
    private sealed class Channel : ILoginCodeChannel
    {
        public string Key => "whatsapp"; public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
        public bool IsAvailable => true; public IReadOnlyList<string> Countries => []; public string? ContactPhone => null;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => Result.Success();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw new NotSupportedException();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
    }
    private sealed class Quotas : IWhatsAppCodeQuotaRepository
    {
        public Action? OnLock { get; set; }
        public Dictionary<(string, DateOnly), WhatsAppCodeQuota> Rows { get; } = [];
        public Dictionary<(Guid, string), WhatsAppCodeQuotaIp> Ips { get; } = [];
        public Task LockAsync(string id, DateOnly day, CancellationToken ct) { OnLock?.Invoke(); OnLock = null; return Task.CompletedTask; }
        public Task<WhatsAppCodeQuota?> GetAsync(string id, DateOnly day, CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault((id, day)));
        public void Add(WhatsAppCodeQuota value) => Rows.Add((value.PhoneNumberId, value.DayUtc), value);
        public Task<WhatsAppCodeQuotaIp?> GetIpAsync(Guid id, string key, CancellationToken ct) => Task.FromResult(Ips.GetValueOrDefault((id, key)));
        public void Add(WhatsAppCodeQuotaIp value) => Ips.Add((value.QuotaId, value.IpKey), value);
        public Task<IReadOnlyList<WhatsAppCodeQuotaIp>> ListExpiredIpsAsync(DateOnly day, int count, CancellationToken ct) => throw new NotSupportedException();
        public void Remove(WhatsAppCodeQuotaIp quota) => throw new NotSupportedException();
    }
}
