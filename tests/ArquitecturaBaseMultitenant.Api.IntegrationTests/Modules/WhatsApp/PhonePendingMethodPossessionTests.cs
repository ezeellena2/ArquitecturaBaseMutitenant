using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class PhonePendingMethodPossessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Phone = "+5491134567890";
    private static object Body => new { phone = new { country = "AR", number = Phone }, channel = "whatsapp" };

    [Fact]
    public async Task Pending_accounts_coexist_until_correct_phone_proof_claims_every_foreign_reservation_once()
    {
        var observer = new ProbeObserver();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => Configure(builder, observer));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var c = await journey.AccountAsync(Ct);
        var first = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        var second = await journey.AddAsync(b.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        await journey.AddAsync(c.Client, Body, Phone, "whatsapp", Ct);
        Assert.Equal(3, (await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct)).Length);
        var before = await journey.OutboxIdsAsync(Ct);
        var wrong = second.Code == "000000" ? "111111" : "000000";
        using var rejected = await PendingMethodJourney.VerifyAsync(b.Client, second.Id, wrong, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(3, (await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct)).Length);
        Assert.Equal(0, observer.Verifications);
        await journey.AssertNoChangeNoticeAsync(before, Ct);
        using var verified = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        var winner = Assert.Single(await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct));
        Assert.Equal(a.Account.UserId, winner.UserId);
        Assert.NotNull(winner.VerifiedAtUtc);
        Assert.False(winner.IsPrimary);
        Assert.Equal(1, observer.Verifications);
        using var lost = await PendingMethodJourney.VerifyAsync(b.Client, second.Id, second.Code, Ct);
        Assert.Equal(HttpStatusCode.NotFound, lost.StatusCode);
        Assert.Equal(1, observer.Verifications);
        Assert.Single((await journey.OutboxIdsAsync(Ct)).Except(before));
        await using var scope = journey.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Users.AsNoTracking().SingleAsync(row => row.Id == a.Account.UserId, Ct);
        Assert.Equal(a.Account.Email.Value, account.Email);
        Assert.Null(account.PhoneNumber);
        Assert.Equal(1, await db.SecurityEvents.CountAsync(row => row.TargetAccountId == a.Account.UserId
            && row.Type == SecurityEventType.LoginMethodChanged, Ct));
    }

    [Fact]
    public async Task Own_phone_reissue_preserves_foreign_reservation_and_its_valid_proof()
    {
        var observer = new ProbeObserver();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => Configure(builder, observer));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var first = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        var foreign = await journey.AddAsync(b.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        var reissued = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        var pending = await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct);
        Assert.Equal(2, pending.Length);
        Assert.DoesNotContain(pending, row => row.Id == first.Id);
        Assert.Contains(pending, row => row.Id == foreign.Id);
        Assert.Contains(pending, row => row.Id == reissued.Id);
        using var verified = await PendingMethodJourney.VerifyAsync(b.Client, foreign.Id, foreign.Code, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Equal(foreign.Id, Assert.Single(await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct)).Id);
        Assert.Equal(1, observer.Verifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expired_phone_proof_removes_only_own_pending_on_verify_or_resend(bool resend)
    {
        var observer = new ProbeObserver();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => Configure(builder, observer));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var first = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        var second = await journey.AddAsync(b.Client, Body, Phone, "whatsapp", Ct);
        journey.Clock.Advance(TimeSpan.FromMinutes(11));
        var before = await journey.OutboxIdsAsync(Ct);
        using var expired = resend
            ? await AccountJourney.PostAsync(b.Client, $"/api/me/login-methods/{second.Id}/code", new { }, Ct)
            : await PendingMethodJourney.VerifyAsync(b.Client, second.Id, second.Code, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        using var problem = await expired.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginCodeErrors.Expired.Code, problem!.RootElement.GetProperty("code").GetString());
        Assert.Equal(first.Id, Assert.Single(await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct)).Id);
        Assert.Equal(0, observer.Verifications);
        await journey.AssertNoChangeNoticeAsync(before, Ct);
    }

    [Fact]
    public async Task Country_withdrawal_preserves_both_pending_proofs_and_emits_no_change_notice()
    {
        var observer = new ProbeObserver();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => Configure(builder, observer));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var first = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        var second = await journey.AddAsync(b.Client, Body, Phone, "whatsapp", Ct);
        var before = await journey.SnapshotAsync(Ct);
        journey.Services.GetRequiredService<IOptions<WhatsAppOptions>>().Value.AllowedCountries = ["US"];
        using var rejected = await PendingMethodJourney.VerifyAsync(b.Client, second.Id, second.Code, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("WhatsApp.Phone.CountryNotAllowed", problem!.RootElement.GetProperty("code").GetString());
        using var resend = await AccountJourney.PostAsync(b.Client, $"/api/me/login-methods/{second.Id}/code", new { }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
        Assert.Equal(before, await journey.SnapshotAsync(Ct));
        Assert.Equal(0, observer.Verifications);
        journey.Services.GetRequiredService<IOptions<WhatsAppOptions>>().Value.AllowedCountries = ["AR"];
        using var verified = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Equal(first.Id, Assert.Single(await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct)).Id);
    }

    [Fact]
    public async Task Concurrent_correct_phone_proofs_have_one_owner_and_one_observer_without_database_conflict()
    {
        var observer = new ProbeObserver();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => Configure(builder, observer));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var first = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        var second = await journey.AddAsync(b.Client, Body, Phone, "whatsapp", Ct);
        var before = await journey.OutboxIdsAsync(Ct);
        var responses = await journey.VerifyTogetherAsync(Phone, a.Client, first.Id, first.Code,
            b.Client, second.Id, second.Code, Ct);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            var loser = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NotFound);
            using var problem = await loser.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal(LoginMethodErrors.NotFound.Code, problem!.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("23505", problem.RootElement.GetRawText(), StringComparison.Ordinal);
            var winner = Assert.Single(await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct));
            Assert.NotNull(winner.VerifiedAtUtc);
            await journey.AssertConsumedAsync(winner.UserId, Phone, true, Ct);
            await journey.AssertConsumedAsync(winner.UserId == a.Account.UserId ? b.Account.UserId : a.Account.UserId, Phone, false, Ct);
            Assert.Equal(1, observer.Verifications);
            Assert.Single((await journey.OutboxIdsAsync(Ct)).Except(before));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Observer_exception_rolls_back_proof_pending_cleanup_contact_and_primary_then_allows_retry()
    {
        var observer = new ProbeObserver();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => Configure(builder, observer));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var first = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        await journey.AddAsync(b.Client, Body, Phone, "whatsapp", Ct);
        var before = await journey.SnapshotAsync(Ct);
        observer.Fail = true;
        using var failed = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(before, await journey.SnapshotAsync(Ct));
        await using (var scope = journey.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Set<ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact>().AnyAsync(row => row.MethodId == first.Id, Ct));
        observer.Fail = false;
        using var retried = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
        Assert.Equal(first.Id, Assert.Single(await journey.MethodsAsync(LoginMethodType.Phone, Phone, Ct)).Id);
        Assert.Equal(2, observer.Verifications);
    }

    [Fact]
    public async Task Phone_verified_owner_race_preserves_field_error_proofs_and_all_reservations_without_emission()
    {
        var observer = new ProbeObserver();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => Configure(builder, observer));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var c = await journey.AccountAsync(Ct);
        var first = await journey.AddAsync(a.Client, Body, Phone, "whatsapp", Ct);
        journey.NextEmission();
        await journey.AddAsync(b.Client, Body, Phone, "whatsapp", Ct);
        await journey.SeedVerifiedOwnerAsync(c.Account.UserId, LoginMethodType.Phone, Phone, Ct);
        var before = await journey.SnapshotAsync(Ct);
        using var rejected = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginMethodErrors.PhoneAlreadyUsed.Code, problem!.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("phone", out _));
        using var add = await AccountJourney.PostAsync(a.Client, "/api/me/login-methods", Body, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode);
        using var resend = await AccountJourney.PostAsync(a.Client, $"/api/me/login-methods/{first.Id}/code", new { }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
        Assert.Equal(before, await journey.SnapshotAsync(Ct));
        Assert.Equal(0, observer.Verifications);
    }

    private static void Configure(IWebHostBuilder builder, ProbeObserver observer) => builder
        .UseSetting("WhatsApp:PhoneNumberId", "pending-possession-number")
        .UseSetting("WhatsApp:AccessToken", "fixture-token")
        .UseSetting("WhatsApp:AppSecret", "fixture-secret")
        .UseSetting("WhatsApp:VerifyToken", "fixture-verify")
        .UseSetting("WhatsApp:AllowedCountries:0", "AR")
        .ConfigureTestServices(services =>
        {
            WhatsAppIdentityTestTransport.BlockExternalSender(services);
            services.AddSingleton<IPhoneLinkObserver>(observer);
        });

    private sealed class ProbeObserver : IPhoneLinkObserver
    {
        private int verifications;
        internal int Verifications => Volatile.Read(ref verifications);
        internal bool Fail { get; set; }
        public Task VerifiedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct)
        {
            Interlocked.Increment(ref verifications);
            if (Fail) throw new InvalidOperationException("Synthetic phone observer failure.");
            return Task.CompletedTask;
        }
        public Task RemovedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct) => Task.CompletedTask;
        public Task AccountDeletedAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;
    }
}

internal static class WhatsAppIdentityTestTransport
{
    internal static void BlockExternalSender(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();
        foreach (var sender in services.Where(descriptor => descriptor.ServiceType == typeof(IChannelSender)
            && descriptor.ImplementationType?.FullName?.Contains(".Modules.WhatsApp.", StringComparison.Ordinal) == true).ToArray())
            services.Remove(sender);
        services.AddSingleton<IChannelSender, ForbiddenSender>();
    }

    private sealed class ForbiddenSender : IChannelSender
    {
        public string Key => "whatsapp";
        public Task SendAsync(string payload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("WhatsApp transport is forbidden in identity and recovery tests.");
    }
}
