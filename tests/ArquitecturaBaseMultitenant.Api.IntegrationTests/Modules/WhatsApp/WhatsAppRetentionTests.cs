using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RetentionWorker = ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Retention.WhatsAppMessageRetentionWorker;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppRetentionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Server_received_age_expires_at_90_days_in_batches_even_when_Meta_time_is_in_the_future_and_channel_is_off()
    {
        await using var host = WhatsAppPrivacyFixture.Host(factory);
        var messages = Enumerable.Range(0, 205).Select(index => Message(91, index % 2 == 0)).Concat([Message(89, true), Message(90, true), Message(91, false)]).ToArray();
        var pending = OutboxMessage.Enqueue("whatsapp", "keep-encrypted-delivery", WhatsAppPrivacyFixture.NowUtc.AddDays(-100));
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, (services, _) =>
        { var db = services.GetRequiredService<ApplicationDbContext>(); db.AddRange(messages); db.Add(pending); return Task.CompletedTask; }, Ct);
        var metadata = messages.Select(Snapshot).ToArray();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            Assert.False(scope.ServiceProvider.GetRequiredService<IWhatsAppAvailability>().IsEnabled);
            var service = scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>();
            Assert.Equal(100, (await service.ClearExpiredTextsAsync(Ct)).Value);
        }
        using var worker = new RetentionWorker(host.Services.GetRequiredService<IServiceScopeFactory>(),
            host.Services.GetRequiredService<ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs.TenantJobRunner>(), host.Services.GetRequiredService<TimeProvider>(), NullLogger<RetentionWorker>.Instance);
        Assert.True(await worker.RunOnceAsync(Ct) >= 107); Assert.Equal(0, await worker.RunOnceAsync(Ct));
        await using var read = host.Services.CreateAsyncScope(); var database = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ids = messages.Select(value => value.Id).ToArray();
        var rows = await database.Set<WhatsAppMessage>().AsNoTracking().Where(value => ids.Contains(value.Id)).ToArrayAsync(Ct);
        Assert.Equal(metadata.Order(), rows.Select(Snapshot).Order());
        Assert.All(rows.Where(value => value.Id != messages[205].Id), value => Assert.Null(value.EncryptedText));
        Assert.NotNull(rows.Single(value => value.Id == messages[205].Id).EncryptedText);
        var outbox = await database.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == pending.Id, Ct);
        Assert.Equal(OutboxStatus.Pending, outbox.Status); Assert.Equal("keep-encrypted-delivery", outbox.EncryptedPayload);
    }
    [Fact]
    public async Task Installed_off_maintenance_runs_at_startup_and_again_after_one_day_without_transport_secrets()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(WhatsAppPrivacyFixture.NowUtc));
        await using var setup = WhatsAppPrivacyFixture.Host(factory, clock);
        var old = Message(91, true); var almost = Message(89, true);
        await WhatsAppPrivacyFixture.TransactionAsync(setup.Services, (services, _) =>
        { services.GetRequiredService<ApplicationDbContext>().AddRange(old, almost); return Task.CompletedTask; }, Ct);
        await using var running = WhatsAppPrivacyFixture.Host(factory, clock).WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddHostedService<RetentionWorker>()));
        Assert.False(running.Services.GetRequiredService<IWhatsAppAvailability>().IsEnabled);
        Assert.Single(running.Services.GetServices<IHostedService>(), service => service is RetentionWorker);
        await WaitUntilClearedAsync(running.Services, old.Id);
        clock.Advance(TimeSpan.FromDays(1));
        await WaitUntilClearedAsync(running.Services, almost.Id);
    }
    [Fact]
    public async Task The_purge_also_clears_the_profile_name_of_a_media_message_without_text()
    {
        await using var host = WhatsAppPrivacyFixture.Host(factory);
        var expired = MediaWithProfile(91); var recent = MediaWithProfile(89);
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, (services, _) =>
        { services.GetRequiredService<ApplicationDbContext>().AddRange(expired, recent); return Task.CompletedTask; }, Ct);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>();
            while ((await service.ClearExpiredTextsAsync(Ct)).Value > 0) { }
        }
        await using var read = host.Services.CreateAsyncScope();
        var rows = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
            .Where(value => value.Id == expired.Id || value.Id == recent.Id).ToDictionaryAsync(value => value.Id, Ct);
        Assert.Null(rows[expired.Id].EncryptedProfileName);
        Assert.Equal(("image", Snapshot(expired)), (rows[expired.Id].ContentType, Snapshot(rows[expired.Id])));
        Assert.Equal("encrypted-profile-name", rows[recent.Id].EncryptedProfileName);
    }
    /// <summary>Una foto sin texto: lo único personal que guarda es el nombre del perfil (C14 de la E12).</summary>
    private static WhatsAppMessage MediaWithProfile(int age) => WhatsAppMessage.Create("retention-number", Guid.NewGuid().ToString("N"), "inbound",
        WhatsAppMessageDirection.Inbound, WhatsAppMessageKind.Unsupported, WhatsAppMessageStatus.Pending, WhatsAppPrivacyFixture.NowUtc,
        WhatsAppPrivacyFixture.NowUtc.AddDays(-age), contentType: "image", encryptedProfileName: "encrypted-profile-name");
    private static WhatsAppMessage Message(int age, bool inbound) => WhatsAppMessage.Create("retention-number", Guid.NewGuid().ToString("N"), inbound ? "inbound" : "accepted",
        inbound ? WhatsAppMessageDirection.Inbound : WhatsAppMessageDirection.Outbound, WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending,
        WhatsAppPrivacyFixture.NowUtc.AddYears(10), WhatsAppPrivacyFixture.NowUtc.AddDays(-age), encryptedText: "encrypted-human-text");
    private static string Snapshot(WhatsAppMessage row) => string.Join('|', row.Id, row.Wamid, row.PhoneNumberId, row.EventType, row.Direction, row.Kind, row.Status,
        row.ReceivedAtUtc.Ticks, row.OccurredAtUtc.Ticks, row.ProcessedAtUtc, row.Attempts, row.LeaseId, row.LeaseUntilUtc, row.NextAttemptAtUtc,
        row.ContactId, row.OriginalUserId, row.OriginalMethodId, row.OutboxMessageId, row.ReplyId);
    private static async Task WaitUntilClearedAsync(IServiceProvider services, Guid id)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var scope = services.CreateAsyncScope();
            if ((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking().SingleAsync(row => row.Id == id, Ct)).EncryptedText is null) return;
            await Task.Delay(25, Ct);
        }
        Assert.Fail("The installed privacy worker did not clear the expired text.");
    }
}
