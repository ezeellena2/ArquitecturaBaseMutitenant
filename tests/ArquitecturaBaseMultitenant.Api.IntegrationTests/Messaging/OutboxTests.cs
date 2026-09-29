using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class OutboxTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Enqueue_requires_the_use_case_transaction_and_a_registered_channel()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outbox = CreateOutbox(context);

        Assert.Throws<InvalidOperationException>(() => outbox.Enqueue(OutboxChannel.Email, "message"));

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await Assert.ThrowsAsync<ArgumentException>(() => unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            outbox.Enqueue("unregistered", "message");
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct));
    }

    [Fact]
    public async Task Enqueue_persists_encrypted_payload_only_after_the_case_use_commits()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var protector = new PayloadProtector(new EphemeralDataProtectionProvider());
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var sender = new TestEmailSender();
        var outbox = new Outbox(context, protector, clock, [sender]);
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var payload = "test-message-" + Guid.CreateVersion7().ToString("N");
        Guid messageId = Guid.Empty;

        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            outbox.Enqueue(OutboxChannel.Email, payload);
            messageId = context.ChangeTracker.Entries<OutboxMessage>().Single().Entity.Id;
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var saved = await context.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == messageId, Ct);
        Assert.Equal(OutboxStatus.Pending, saved.Status);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, saved.CreatedAtUtc);
        Assert.DoesNotContain(payload, saved.EncryptedPayload, StringComparison.Ordinal);
        Assert.Equal(payload, protector.Unprotect(saved.EncryptedPayload));
        Assert.DoesNotContain(payload, saved.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, sender.SendCount);
    }

    [Fact]
    public async Task Failed_use_case_does_not_persist_the_message()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var outbox = CreateOutbox(context);
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        Guid messageId = Guid.Empty;

        var result = await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            outbox.Enqueue(OutboxChannel.Email, "rollback message");
            messageId = context.ChangeTracker.Entries<OutboxMessage>().Single().Entity.Id;
            return Task.FromResult(Result.Failure(Error.Conflict("Test.Rollback", "Abort test transaction.")));
        }, CommitPolicy.OnSuccess, Ct);

        Assert.True(result.IsFailure);
        Assert.False(await context.OutboxMessages.AsNoTracking().AnyAsync(message => message.Id == messageId, Ct));
    }

    private static Outbox CreateOutbox(ApplicationDbContext context) =>
        new(context, new PayloadProtector(new EphemeralDataProtectionProvider()),
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero)),
            [new TestEmailSender()]);

    private sealed class TestEmailSender : IChannelSender
    {
        public string Key => OutboxChannel.Email;

        public int SendCount { get; private set; }

        public Task SendAsync(string payload, CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.CompletedTask;
        }
    }
}
