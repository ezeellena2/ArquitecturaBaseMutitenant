using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>Control privado contra outbox cifrado y tickets persistidos reales; ningún cambio alcanza otro escenario.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppBrowserControlTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ciphertext_read_filters_destination_and_scenario_cursor()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        await using var app = factory.WithWebHostBuilder(builder => builder
            .UseSetting("WhatsApp:PhoneNumberId", WhatsAppBrowserControl.NumberId)
            .UseSetting("WhatsApp:AccessToken", "browser-fixture-token")
            .UseSetting("WhatsApp:AppSecret", WhatsAppBrowserControl.AppSecret)
            .UseSetting("WhatsApp:VerifyToken", "browser-fixture-verify")
            .UseSetting("WhatsApp:ProcessInboundInBackground", "false")
            .ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)).ConfigureLogging(logging => logging.ClearProviders()));
        using var client = app.CreateClient();
        await EnqueueAsync(app.Services, "+5491134567880", "111111");
        clock.Advance(TimeSpan.FromSeconds(1));
        var control = new WhatsAppBrowserControl(app.Services);
        var request = new WhatsAppBrowserRequest("begin", "outbox", "+5491134567880");
        await control.ExecuteAsync(request, client, Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(request with { Kind = "code" }, client, Ct));
        await EnqueueAsync(app.Services, request.Phone, "222222");
        clock.Advance(TimeSpan.FromSeconds(1));
        await EnqueueAsync(app.Services, "+5491134567881", "333333");
        var reply = JsonSerializer.SerializeToElement(await control.ExecuteAsync(request with { Kind = "code" }, client, Ct));
        Assert.Equal("222222", reply.GetProperty("proof").GetString());
    }

    [Fact]
    public async Task Expiring_a_ticket_requires_the_exact_hash_current_owner_and_cursor()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 13, 0, 0, TimeSpan.Zero));
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)).ConfigureLogging(logging => logging.ClearProviders()));
        using var client = app.CreateClient();
        var control = new WhatsAppBrowserControl(app.Services);
        var request = new WhatsAppBrowserRequest("begin", "ticket", "+5491134567882");
        var tokens = app.Services.GetRequiredService<ISecureTokenGenerator>();
        var oldToken = tokens.Generate(); var ownToken = tokens.Generate(); var otherToken = tokens.Generate();
        await PrepareTicketAsync(app.Services, request.Phone, oldToken, clock.GetUtcNow().UtcDateTime);
        clock.Advance(TimeSpan.FromSeconds(1));
        await control.ExecuteAsync(request, client, Ct);
        await PrepareTicketAsync(app.Services, request.Phone, ownToken, clock.GetUtcNow().UtcDateTime);
        await PrepareTicketAsync(app.Services, "+5491134567883", otherToken, clock.GetUtcNow().UtcDateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(request with { Kind = "expireTicket", Ticket = oldToken }, client, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(request with { Kind = "expireTicket", Ticket = otherToken }, client, Ct));
        await control.ExecuteAsync(request with { Kind = "expireTicket", Ticket = ownToken }, client, Ct);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<ISecureTokenGenerator>();
        var ownHash = hash.Hash(ownToken); var otherHash = hash.Hash(otherToken); var oldHash = hash.Hash(oldToken);
        Assert.True(await db.AccountDeletionCancelTickets.AnyAsync(ticket => ticket.TokenHash == ownHash && ticket.ExpiresAtUtc < clock.GetUtcNow().UtcDateTime, Ct));
        Assert.Equal(2, await db.AccountDeletionCancelTickets.CountAsync(ticket => (ticket.TokenHash == otherHash || ticket.TokenHash == oldHash) && ticket.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime, Ct));
    }

    private static async Task EnqueueAsync(IServiceProvider services, string phone, string code)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            provider.GetRequiredService<IOutbox>().Enqueue("whatsapp", JsonSerializer.Serialize(new { To = phone, TemplateName = "codigo_ingreso", BodyParameters = new[] { code } }));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task PrepareTicketAsync(IServiceProvider services, string phone, string token, DateTime nowUtc)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = provider.GetRequiredService<ApplicationDbContext>();
            var userId = await db.LoginMethods.Where(method => method.Type == LoginMethodType.Phone && method.Value == phone).Select(method => (Guid?)method.UserId).SingleOrDefaultAsync(ct);
            if (userId is null)
            {
                userId = (await provider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "UTC", ct)).Id;
                var method = LoginMethod.CreatePhone(userId.Value, PhoneNumber.Create(phone).Value); method.Verify(nowUtc);
                provider.GetRequiredService<ILoginMethodRepository>().Add(method);
            }
            db.AccountDeletionCancelTickets.Add(AccountDeletionCancelTicket.Issue(userId.Value, provider.GetRequiredService<ISecureTokenGenerator>().Hash(token), nowUtc, "/login/enlace"));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }
}
