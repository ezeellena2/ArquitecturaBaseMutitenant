using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using System.Text.Json;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppOutboxTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Encrypted_transient_payload_confirms_atomic_metadata_without_resend(bool authentication)
    {
        using var api = factory.CreateClient();
        var wamid = "wamid.t03." + Guid.NewGuid().ToString("N");
        using var handler = new Handler(factory, wamid);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var sender = new WhatsAppChannelSender(new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions { AccessToken = "fixture-token" }), TimeProvider.System));
        var culture = await new CultureProfiles(new JsonReferenceDataCatalog()).LoadAsync("es-AR", Ct);
        Guid id;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var outbox = new Outbox(context, seed.ServiceProvider.GetRequiredService<IPayloadProtector>(), TimeProvider.System, [sender]);
            await seed.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                if (authentication) new WhatsAppLoginCodeChannel(new Settings(), WhatsAppTemplateTests.Renderer(), outbox)
                    .EnqueueVerification(null, "+5491134567890", "483920", 10, culture);
                else await new WhatsAppAccountNoticeChannel(new Settings(), WhatsAppTemplateTests.Renderer(), outbox)
                    .EnqueueAsync(null, "+5491134567890", new AccountNotice.RecoveryApproved("https://example.test/login#transient-secret"), culture, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            var row = Assert.Single(context.OutboxMessages.Local);
            id = row.Id;
            Assert.DoesNotContain("483920", row.EncryptedPayload);
            Assert.DoesNotContain("transient-secret", row.EncryptedPayload);
            Assert.DoesNotContain("+5491134567890", row.EncryptedPayload);
        }
        var processor = new OutboxDeliveryProcessor(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<IPayloadProtector>(), TimeProvider.System, Options.Create(new OutboxOptions()), NullLogger<OutboxDeliveryProcessor>.Instance);
        Assert.Equal(1, await processor.ProcessOnceAsync([sender], Ct));
        Assert.Equal(0, await processor.ProcessOnceAsync([sender], Ct));
        Assert.Equal(1, handler.Calls);
        await using var restart = factory.Services.CreateAsyncScope();
        var saved = restart.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var delivered = await saved.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct);
        Assert.Equal(OutboxStatus.Sent, delivered.Status);
        Assert.Equal(wamid, delivered.ProviderMessageId);
        Assert.Empty(delivered.EncryptedPayload);
        var metadata = await saved.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Wamid == wamid, Ct);
        Assert.Equal(id, metadata.OutboxMessageId);
        Assert.Equal(authentication ? WhatsAppMessageKind.Authentication : WhatsAppMessageKind.Template, metadata.Kind);
        Assert.Equal(WhatsAppMessageDirection.Outbound, metadata.Direction);
        Assert.Null(metadata.EncryptedText);
        Assert.Null(metadata.ContactId);
        Assert.NotNull(metadata.ProcessedAtUtc);
    }
    [Fact]
    public async Task Queue_delayed_past_inbound_window_rejects_terminally_and_erases_payload_without_HTTP()
    {
        using var api = factory.CreateClient();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        using var handler = new Handler(factory, "wamid.must-not-send");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var sender = new WhatsAppChannelSender(new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions { AccessToken = "fixture-token" }), clock));
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var outbox = new Outbox(context, scope.ServiceProvider.GetRequiredService<IPayloadProtector>(), clock, [sender]);
            var reply = new WhatsAppOutboundMessage("number", "+5491134567890", "", "es", [], Body: "Reply", ReplyExpiresAtUtc: clock.GetUtcNow().UtcDateTime.AddHours(24));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            { outbox.Enqueue("whatsapp", JsonSerializer.Serialize(reply)); return Task.FromResult(Result.Success()); }, CommitPolicy.OnSuccess, Ct);
            id = Assert.Single(context.OutboxMessages.Local).Id;
        }
        clock.Advance(TimeSpan.FromHours(25));
        var processor = new OutboxDeliveryProcessor(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<IPayloadProtector>(), clock, Options.Create(new OutboxOptions()), NullLogger<OutboxDeliveryProcessor>.Instance);
        Assert.Equal(1, await processor.ProcessOnceAsync([sender], Ct)); Assert.Equal(0, await processor.ProcessOnceAsync([sender], Ct));
        Assert.Equal(0, handler.Calls);
        await using var read = factory.Services.CreateAsyncScope();
        var saved = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct);
        Assert.Equal(OutboxStatus.Failed, saved.Status); Assert.Equal("WhatsApp.Send.WindowExpired", saved.LastErrorCode);
        Assert.Null(saved.NextAttemptAtUtc); Assert.Empty(saved.EncryptedPayload);
    }
    [Fact]
    public async Task Business_rollback_does_not_leave_an_outbound_payload()
    {
        using var api = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var before = await context.OutboxMessages.CountAsync(Ct);
        using var handler = new Handler(factory, "wamid.not-sent");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var sender = new WhatsAppChannelSender(new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions()), TimeProvider.System));
        var outbox = new Outbox(context, scope.ServiceProvider.GetRequiredService<IPayloadProtector>(), TimeProvider.System, [sender]);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        { outbox.Enqueue("whatsapp", "transient-secret"); return Task.FromResult(Result.Failure(Error.Conflict("Test.Flow.Rollback", "Rollback."))); }, CommitPolicy.OnSuccess, Ct);
        await using var restart = factory.Services.CreateAsyncScope();
        Assert.Equal(before, await restart.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.CountAsync(Ct));
        Assert.Equal(0, handler.Calls);
    }
    private sealed class Settings : IWhatsAppChannelSettings
    {
        public string AppName => "Fixture";
        public bool IsEnabled => true; public string? PhoneNumberId => "template-number"; public string? ContactPhone => null;
        public IReadOnlyList<string> Countries => ["AR"]; public int DailyAuthCodeLimit => 100;
        public Result ValidateNewDestination(LoginCodeDestination destination) => throw new InvalidOperationException("Verified methods do not apply new-country policy.");
    }
    private sealed class Handler(ApiFactory factory, string wamid) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await using var read = factory.Services.CreateAsyncScope();
            var context = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Single(await context.OutboxMessages.AsNoTracking().Where(value => value.Channel == "whatsapp" && value.Status == OutboxStatus.Pending
                && value.LeaseId != null).ToArrayAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"" + wamid + "\"}]}") };
        }
    }
}
