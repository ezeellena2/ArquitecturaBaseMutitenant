using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>La reserva concurrente, rollback y purga no dependen del historial de OTP.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppCodeQuotaPersistenceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 23, 59, 58, TimeSpan.Zero));
    private readonly Settings _settings = new();
    private static LoginCodeDestination Destination => LoginCodeDestination.ForPhone(PhoneNumber.Create("+5491134567890").Value, "whatsapp");
    [Fact]
    public async Task Two_emitters_compete_for_last_slot_and_UTC_rollover_renews_it()
    {
        using var client = factory.CreateClient();
        var reservations = await Task.WhenAll(ReserveAsync(), ReserveAsync());
        Assert.Single(reservations, value => value.IsSuccess);
        Assert.Single(reservations, value => value.IsFailure && value.Error.Code == WhatsAppCodeErrors.DailyLimitCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, (await context.Set<WhatsAppCodeQuota>().SingleAsync(value => value.PhoneNumberId == _settings.PhoneNumberId, Ct)).ReservedCount);
        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True((await ReserveAsync()).IsSuccess);
        Assert.Equal(2, await context.Set<WhatsAppCodeQuota>().CountAsync(value => value.PhoneNumberId == _settings.PhoneNumberId, Ct));
    }
    [Fact]
    public async Task Rollback_does_not_spend_and_purging_OTP_cannot_reset_reserved_quota()
    {
        using var client = factory.CreateClient();
        Assert.True((await ReserveAsync(abort: true)).IsFailure);
        await using var scope = factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider; var context = provider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.Set<WhatsAppCodeQuota>().AnyAsync(value => value.PhoneNumberId == _settings.PhoneNumberId, Ct));
        var unit = provider.GetRequiredService<IUnitOfWork>();
        Guid userId = Guid.Empty;
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            userId = (await provider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var issuer = new LoginCodeIssuer(provider.GetRequiredService<ILoginCodeRepository>(), provider.GetRequiredService<ILoginCodeGenerator>(),
                provider.GetRequiredService<ILoginCodeHasher>(), Options.Create(new LoginCodeOptions()), _clock, [Admission(provider)]);
            var code = await issuer.IssueAccountCodeAsync(Destination, LoginCodePurpose.VerifyDestination, userId, ct);
            return code.IsSuccess ? Result.Success() : Result.Failure(code.Error);
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(await context.LoginCodes.AnyAsync(value => value.RequestedByUserId == userId, Ct));
        await unit.ExecuteInTransactionAsync(async ct =>
        { await provider.GetRequiredService<IAccountDeletionRepository>().PurgeCredentialsAsync(userId, ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
        Assert.False(await context.LoginCodes.AnyAsync(value => value.RequestedByUserId == userId, Ct));
        Assert.Equal(WhatsAppCodeErrors.DailyLimitCode, (await ReserveAsync()).Error.Code);
        Assert.Equal(1, (await context.Set<WhatsAppCodeQuota>().AsNoTracking().SingleAsync(value => value.PhoneNumberId == _settings.PhoneNumberId, Ct)).ReservedCount);
    }
    private WhatsAppCodeQuotaIssuer Admission(IServiceProvider provider) => new(provider.GetRequiredService<IWhatsAppCodeQuotaRepository>(), _settings, _clock);

    [Fact]
    public async Task HTTP_idempotent_replay_returns_original_success_without_reserving_again()
    {
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/test/whatsapp-quota/" + _settings.PhoneNumberId);
            request.Headers.Add("Idempotency-Key", key.ToString("D"));
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal(attempt > 0, response.Headers.Contains("Idempotent-Replayed"));
        }
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppCodeQuota>()
            .SingleAsync(value => value.PhoneNumberId == _settings.PhoneNumberId, Ct)).ReservedCount);
    }
    private async Task<Result> ReserveAsync(bool abort = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var result = await Admission(scope.ServiceProvider).ReserveAsync(Destination, ct);
            return abort ? Result.Failure(Error.Conflict("Test.Transaction.Abort", "Fixture rollback.")) : result;
        }, CommitPolicy.OnSuccess, Ct);
    }
    private sealed class Settings : IWhatsAppChannelSettings
    {
        public string AppName => "Fixture";
        public bool IsEnabled => true;
        public string PhoneNumberId { get; } = Guid.NewGuid().ToString("N");
        public string? ContactPhone => null;
        public IReadOnlyList<string> Countries => ["AR"];
        public int DailyAuthCodeLimit => 1;
        public Result ValidateNewDestination(LoginCodeDestination destination) => Result.Success();
    }
}
