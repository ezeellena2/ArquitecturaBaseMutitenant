using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppAccountLifecycleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Repeated_original_owner_cleanup_leaves_the_next_owner_and_anonymous_text_intact()
    {
        await using var host = WhatsAppPrivacyFixture.Host(factory);
        var original = await WhatsAppPrivacyFixture.SeedAsync(host.Services, Ct); var next = await WhatsAppPrivacyFixture.SeedAsync(host.Services, Ct);
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            (await db.Set<WhatsAppContact>().SingleAsync(value => value.Id == original.ContactId, ct)).Link(next.MethodId, next.UserId);
        }, Ct);
        for (var repeat = 0; repeat < 2; repeat++)
            await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
            {
                await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(original.UserId, ct);
                var participant = Assert.Single(services.GetServices<IAccountDeletionParticipant>(), value => value.GetType().Name == "WhatsAppAccountDeletionParticipant");
                var context = new AccountDeletionContext(original.UserId, WhatsAppPrivacyFixture.NowUtc);
                await participant.ExecuteAsync(context with { TenantId = Guid.CreateVersion7() }, ct);
                await participant.ExecuteAsync(context, ct);
            }, Ct);
        await using var read = host.Services.CreateAsyncScope(); var database = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(next.UserId, (await database.Set<WhatsAppContact>().SingleAsync(value => value.Id == original.ContactId, Ct)).UserId);
        var old = await database.Set<WhatsAppMessage>().SingleAsync(value => value.Id == original.MessageId, Ct);
        Assert.Null(old.ContactId); Assert.Null(old.EncryptedText); Assert.Equal(original.UserId, old.OriginalUserId); Assert.Equal(original.MethodId, old.OriginalMethodId);
        Assert.NotNull((await database.Set<WhatsAppMessage>().SingleAsync(value => value.Id == original.AnonymousId, Ct)).EncryptedText);
        Assert.NotNull((await database.Set<WhatsAppMessage>().SingleAsync(value => value.Id == next.MessageId, Ct)).EncryptedText);
    }
    [Fact]
    public async Task Final_core_deletion_purges_phone_and_contact_but_preserves_new_encrypted_final_notice()
    {
        await using var host = WhatsAppPrivacyFixture.Host(factory, enabled: true);
        var original = await WhatsAppPrivacyFixture.SeedAsync(host.Services, Ct);
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            Assert.True((await services.GetRequiredService<IUserRepository>().RequestDeletionAsync(original.UserId, "Fixture privacy deletion",
                WhatsAppPrivacyFixture.NowUtc.AddDays(-31), 30, ct)).IsSuccess);
            services.GetRequiredService<ApplicationDbContext>().OutboxMessages.Add(OutboxMessage.Enqueue("whatsapp", "old-encrypted-notice", WhatsAppPrivacyFixture.NowUtc, original.UserId));
        }, Ct);
        await using (var claimScope = host.Services.CreateAsyncScope())
        {
            var claimed = await claimScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ClaimNextAsync(Ct);
            Assert.True(claimed.IsSuccess); Assert.NotNull(claimed.Value); Assert.Equal(original.UserId, claimed.Value.UserId);
            await using var complete = host.Services.CreateAsyncScope();
            Assert.True((await complete.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().CompleteAsync(claimed.Value, Ct)).IsSuccess);
        }
        await using var read = host.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(UserStatus.Deleted, (await db.Users.SingleAsync(value => value.Id == original.UserId, Ct)).Status);
        Assert.False(await db.LoginMethods.AnyAsync(value => value.UserId == original.UserId, Ct));
        Assert.False(await db.Set<WhatsAppContact>().AnyAsync(value => value.Id == original.ContactId, Ct));
        var row = await db.Set<WhatsAppMessage>().SingleAsync(value => value.Id == original.MessageId, Ct);
        Assert.Null(row.EncryptedText); Assert.Null(row.ContactId); Assert.Equal(original.UserId, row.OriginalUserId);
        var notices = await db.OutboxMessages.Where(value => value.UserId == original.UserId).ToArrayAsync(Ct);
        Assert.Equal(OutboxStatus.Cancelled, Assert.Single(notices, value => value.Status == OutboxStatus.Cancelled).Status);
        var final = Assert.Single(notices, value => value.Status == OutboxStatus.Pending);
        Assert.NotEmpty(final.EncryptedPayload); Assert.DoesNotContain(original.Phone.Value, final.EncryptedPayload, StringComparison.Ordinal);
        Assert.Contains("cuenta_eliminada", read.ServiceProvider.GetRequiredService<IPayloadProtector>().Unprotect(final.EncryptedPayload), StringComparison.Ordinal);
    }
}
