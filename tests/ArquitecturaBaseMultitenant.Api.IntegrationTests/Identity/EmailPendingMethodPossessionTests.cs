using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

public sealed class EmailPendingMethodPossessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pending_accounts_coexist_until_correct_proof_claims_all_foreign_reservations()
    {
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct);
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var c = await journey.AccountAsync(Ct);
        var email = "pending-" + Guid.NewGuid().ToString("N") + "@example.test";
        var first = await journey.AddAsync(a.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        var second = await journey.AddAsync(b.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        await journey.AddAsync(c.Client, new { email }, email, "email", Ct);
        var pending = await journey.MethodsAsync(LoginMethodType.Email, email, Ct);
        Assert.Equal(3, pending.Length);
        Assert.All(pending, method => Assert.Null(method.VerifiedAtUtc));
        var before = await journey.OutboxIdsAsync(Ct);
        var wrong = new[] { "000000", "111111", "222222" }.First(value => value != first.Code && value != second.Code);
        using var rejected = await PendingMethodJourney.VerifyAsync(b.Client, second.Id, wrong, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(pending.Select(row => row.Id), (await journey.MethodsAsync(LoginMethodType.Email, email, Ct)).Select(row => row.Id));
        await journey.AssertNoChangeNoticeAsync(before, Ct);

        using var verified = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        var winner = Assert.Single(await journey.MethodsAsync(LoginMethodType.Email, email, Ct));
        Assert.Equal(a.Account.UserId, winner.UserId);
        Assert.NotNull(winner.VerifiedAtUtc);
        Assert.False(winner.IsPrimary);
        using var lost = await PendingMethodJourney.VerifyAsync(b.Client, second.Id, second.Code, Ct);
        Assert.Equal(HttpStatusCode.NotFound, lost.StatusCode);
        using var problem = await lost.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginMethodErrors.NotFound.Code, problem!.RootElement.GetProperty("code").GetString());
        await using var scope = journey.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(a.Account.Email.Value, (await db.Users.AsNoTracking().SingleAsync(row => row.Id == a.Account.UserId, Ct)).Email);
        Assert.Single((await journey.OutboxIdsAsync(Ct)).Except(before));
    }

    [Fact]
    public async Task Own_reissue_replaces_only_own_pending_and_invalidates_only_own_previous_proof()
    {
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct);
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var email = "pending-" + Guid.NewGuid().ToString("N") + "@example.test";
        var first = await journey.AddAsync(a.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        var foreign = await journey.AddAsync(b.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        var reissued = await journey.AddAsync(a.Client, new { email }, email, "email", Ct);
        var pending = await journey.MethodsAsync(LoginMethodType.Email, email, Ct);
        Assert.Equal(2, pending.Length);
        Assert.DoesNotContain(pending, row => row.Id == first.Id);
        Assert.Contains(pending, row => row.Id == foreign.Id);
        Assert.Contains(pending, row => row.Id == reissued.Id);
        await using var scope = journey.Services.CreateAsyncScope();
        var codes = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes.AsNoTracking()
            .Where(row => row.Destination == email && row.Purpose == LoginCodePurpose.VerifyDestination).ToArrayAsync(Ct);
        Assert.NotNull(Assert.Single(codes, row => row.RequestedByUserId == a.Account.UserId && row.InvalidatedAtUtc != null).InvalidatedAtUtc);
        Assert.Null(Assert.Single(codes, row => row.RequestedByUserId == b.Account.UserId).InvalidatedAtUtc);
        using var verified = await PendingMethodJourney.VerifyAsync(b.Client, foreign.Id, foreign.Code, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Equal(foreign.Id, Assert.Single(await journey.MethodsAsync(LoginMethodType.Email, email, Ct)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expired_own_proof_removes_only_own_pending_on_verify_or_resend(bool resend)
    {
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct);
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var email = "pending-" + Guid.NewGuid().ToString("N") + "@example.test";
        var first = await journey.AddAsync(a.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        var second = await journey.AddAsync(b.Client, new { email }, email, "email", Ct);
        journey.Clock.Advance(TimeSpan.FromMinutes(11));
        var before = await journey.OutboxIdsAsync(Ct);
        using var expired = resend
            ? await AccountJourney.PostAsync(b.Client, $"/api/me/login-methods/{second.Id}/code", new { }, Ct)
            : await PendingMethodJourney.VerifyAsync(b.Client, second.Id, second.Code, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        using var problem = await expired.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginCodeErrors.Expired.Code, problem!.RootElement.GetProperty("code").GetString());
        Assert.Equal(first.Id, Assert.Single(await journey.MethodsAsync(LoginMethodType.Email, email, Ct)).Id);
        await journey.AssertNoChangeNoticeAsync(before, Ct);
    }

    [Fact]
    public async Task Concurrent_correct_proofs_have_one_owner_and_a_safe_missing_method_loser()
    {
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct);
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var email = "pending-" + Guid.NewGuid().ToString("N") + "@example.test";
        var first = await journey.AddAsync(a.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        var second = await journey.AddAsync(b.Client, new { email }, email, "email", Ct);
        var before = await journey.OutboxIdsAsync(Ct);
        var responses = await journey.VerifyTogetherAsync(email, a.Client, first.Id, first.Code,
            b.Client, second.Id, second.Code, Ct);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            var loser = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NotFound);
            using var problem = await loser.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal(LoginMethodErrors.NotFound.Code, problem!.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("23505", problem.RootElement.GetRawText(), StringComparison.Ordinal);
            var winner = Assert.Single(await journey.MethodsAsync(LoginMethodType.Email, email, Ct));
            Assert.NotNull(winner.VerifiedAtUtc);
            await journey.AssertConsumedAsync(winner.UserId, email, true, Ct);
            await journey.AssertConsumedAsync(winner.UserId == a.Account.UserId ? b.Account.UserId : a.Account.UserId, email, false, Ct);
            Assert.Single((await journey.OutboxIdsAsync(Ct)).Except(before));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Notice_exception_rolls_back_proof_verification_pending_cleanup_and_event()
    {
        var failure = new NoticeFailure();
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct, builder => builder.ConfigureTestServices(services =>
        {
            var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IAccountNoticeChannel)
                && item.ImplementationType?.Namespace?.Contains(".Modules.", StringComparison.Ordinal) == false);
            services.Remove(descriptor);
            services.Insert(0, ServiceDescriptor.Scoped<IAccountNoticeChannel>(provider =>
                new FailingNotice( (IAccountNoticeChannel)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!), failure)));
        }));
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var email = "pending-" + Guid.NewGuid().ToString("N") + "@example.test";
        var first = await journey.AddAsync(a.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        await journey.AddAsync(b.Client, new { email }, email, "email", Ct);
        var before = await journey.SnapshotAsync(Ct);
        failure.Enabled = true;
        using var failed = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(before, await journey.SnapshotAsync(Ct));
        failure.Enabled = false;
        using var retried = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
        Assert.Equal(first.Id, Assert.Single(await journey.MethodsAsync(LoginMethodType.Email, email, Ct)).Id);
    }

    [Fact]
    public async Task Email_verified_owner_race_keeps_invalid_proof_behavior_and_all_pending()
    {
        await using var journey = new PendingMethodJourney();
        await journey.InitializeAsync(Ct);
        var a = await journey.AccountAsync(Ct);
        var b = await journey.AccountAsync(Ct);
        var c = await journey.AccountAsync(Ct);
        var email = "pending-" + Guid.NewGuid().ToString("N") + "@example.test";
        var first = await journey.AddAsync(a.Client, new { email }, email, "email", Ct);
        journey.NextEmission();
        await journey.AddAsync(b.Client, new { email }, email, "email", Ct);
        await journey.SeedVerifiedOwnerAsync(c.Account.UserId, LoginMethodType.Email, email, Ct);
        var before = await journey.OutboxIdsAsync(Ct);
        using var rejected = await PendingMethodJourney.VerifyAsync(a.Client, first.Id, first.Code, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginCodeErrors.Invalid(4).Code, problem!.RootElement.GetProperty("code").GetString());
        var methods = await journey.MethodsAsync(LoginMethodType.Email, email, Ct);
        Assert.Equal(3, methods.Length);
        Assert.Equal(c.Account.UserId, Assert.Single(methods, row => row.VerifiedAtUtc != null).UserId);
        await journey.AssertConsumedAsync(a.Account.UserId, email, false, Ct);
        await journey.AssertNoChangeNoticeAsync(before, Ct);
        await using var scope = journey.Services.CreateAsyncScope();
        Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes.AsNoTracking()
            .SingleAsync(row => row.RequestedByUserId == a.Account.UserId && row.Destination == email, Ct)).FailedAttempts);
    }

    private sealed class NoticeFailure { internal bool Enabled { get; set; } }
    private sealed class FailingNotice(IAccountNoticeChannel inner, NoticeFailure failure) : IAccountNoticeChannel
    {
        public string Key => inner.Key;
        public LoginMethodType MethodType => inner.MethodType;
        public Task EnqueueAsync(Guid? userId, string destination, AccountNotice notice, CultureProfile culture, CancellationToken ct) =>
            failure.Enabled ? throw new InvalidOperationException("Synthetic account notice failure.")
                : inner.EnqueueAsync(userId, destination, notice, culture, ct);
    }
}
