using System.Data.Common;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

public sealed class OutboxDeletionRaceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Pending_selection_locks_before_cancellation_and_completion_cannot_overwrite_it()
    {
        var barrier = new SelectionBarrier();
        await using var host = OutboxPrivacyFixture.Host(factory).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(barrier))));
        var user = Guid.CreateVersion7(); var channel = "race-" + Guid.NewGuid().ToString("N");
        var row = OutboxMessage.Enqueue(channel, "encrypted-notice", OutboxPrivacyFixture.NowUtc, user);
        await OutboxPrivacyFixture.TransactionAsync(host.Services, (services, _) =>
        { services.GetRequiredService<ApplicationDbContext>().Add(row); return Task.CompletedTask; }, Ct);
        await using var dispatch = host.Services.CreateAsyncScope();
        var service = dispatch.ServiceProvider.GetRequiredService<IOutboxDispatchService>();
        var claim = (await service.ClaimNextAsync([channel], Ct)).Value!; Assert.NotNull(claim);
        barrier.Armed = true;
        var deleting = OutboxPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            await new OutboxDeletionParticipant(services.GetRequiredService<ApplicationDbContext>())
                .ExecuteAsync(new AccountDeletionContext(user, OutboxPrivacyFixture.NowUtc), ct);
        }, Ct);
        await barrier.Selected.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var completing = service.CompleteAsync(row.Id, claim.LeaseId, OutboxDeliveryReceipt.Accepted("receipt"), Ct);
        try
        {
            // Consulta PostgreSQL: Complete realmente espera el lock que protege la selección Pending.
            // Sin FOR UPDATE, Complete confirma Sent mientras el reader de baja está pausado.
            await WaitForCompletionLockAsync(completing);
            Assert.False(completing.IsCompleted);
        }
        finally { barrier.Release.TrySetResult(); await deleting; await completing; }
        await deleting; Assert.True((await completing).IsSuccess);
        await using var read = host.Services.CreateAsyncScope();
        var saved = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == row.Id, Ct);
        Assert.Equal(OutboxStatus.Cancelled, saved.Status); Assert.Null(saved.LeaseId); Assert.Null(saved.LeaseExpiresAtUtc);
        Assert.Null((await service.ClaimNextAsync([channel], Ct)).Value);
    }
    [Fact]
    public async Task Already_confirmed_sent_is_preserved_even_if_deletion_context_tracked_pending()
    {
        await using var host = OutboxPrivacyFixture.Host(factory);
        var user = Guid.CreateVersion7(); var channel = "sent-" + Guid.NewGuid().ToString("N");
        var row = OutboxMessage.Enqueue(channel, "encrypted-notice", OutboxPrivacyFixture.NowUtc, user);
        await OutboxPrivacyFixture.TransactionAsync(host.Services, (services, _) =>
        { services.GetRequiredService<ApplicationDbContext>().Add(row); return Task.CompletedTask; }, Ct);
        await using var deletion = host.Services.CreateAsyncScope(); var database = deletion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(OutboxStatus.Pending, (await database.OutboxMessages.SingleAsync(value => value.Id == row.Id, Ct)).Status);
        await using var dispatch = host.Services.CreateAsyncScope(); var service = dispatch.ServiceProvider.GetRequiredService<IOutboxDispatchService>();
        var claim = (await service.ClaimNextAsync([channel], Ct)).Value!;
        Assert.True((await service.CompleteAsync(row.Id, claim.LeaseId, OutboxDeliveryReceipt.Accepted("receipt"), Ct)).IsSuccess);
        await deletion.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await deletion.ServiceProvider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            await new OutboxDeletionParticipant(database).ExecuteAsync(new AccountDeletionContext(user, OutboxPrivacyFixture.NowUtc), ct);
            return Domain.Results.Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var saved = await database.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == row.Id, Ct);
        Assert.Equal(OutboxStatus.Sent, saved.Status); Assert.Equal("receipt", saved.ProviderMessageId);
        Assert.Null((await service.ClaimNextAsync([channel], Ct)).Value);
    }
    [Fact]
    public async Task Cancellation_reloads_a_lease_acquired_after_the_context_tracked_the_pending_row()
    {
        await using var host = OutboxPrivacyFixture.Host(factory);
        var user = Guid.CreateVersion7(); var channel = "reload-" + Guid.NewGuid().ToString("N");
        var row = OutboxMessage.Enqueue(channel, "encrypted-notice", OutboxPrivacyFixture.NowUtc, user);
        await OutboxPrivacyFixture.TransactionAsync(host.Services, (services, _) =>
        { services.GetRequiredService<ApplicationDbContext>().Add(row); return Task.CompletedTask; }, Ct);
        await using var deletion = host.Services.CreateAsyncScope(); var database = deletion.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await database.OutboxMessages.SingleAsync(value => value.Id == row.Id, Ct)).LeaseId);
        await using var dispatch = host.Services.CreateAsyncScope(); var service = dispatch.ServiceProvider.GetRequiredService<IOutboxDispatchService>();
        var claim = (await service.ClaimNextAsync([channel], Ct)).Value!; Assert.NotNull(claim);
        await deletion.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await deletion.ServiceProvider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            await new OutboxDeletionParticipant(database).ExecuteAsync(new AccountDeletionContext(user, OutboxPrivacyFixture.NowUtc), ct);
            return Domain.Results.Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var saved = await database.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == row.Id, Ct);
        Assert.Equal(OutboxStatus.Cancelled, saved.Status); Assert.Null(saved.LeaseId); Assert.Null(saved.LeaseExpiresAtUtc); Assert.Empty(saved.EncryptedPayload);
        Assert.True((await service.CompleteAsync(row.Id, claim.LeaseId, OutboxDeliveryReceipt.Accepted("late"), Ct)).IsSuccess);
        Assert.Equal(OutboxStatus.Cancelled, (await database.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == row.Id, Ct)).Status);
    }
    private async Task WaitForCompletionLockAsync(Task completing)
    {
        // PostgreSQL oculta la consulta de mt_app a mt_owner; el bootstrap permite observar sus locks.
        await using var connection = new NpgsqlConnection(factory.BootstrapConnectionString); await connection.OpenAsync(Ct);
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (completing.IsCompleted) return;
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%OutboxMessages%')";
            if ((bool)(await command.ExecuteScalarAsync(Ct))!) return;
            await Task.Delay(10, Ct);
        }
        Assert.Fail("The concurrent completion did not wait for the selected outbox row.");
    }
    private sealed class SelectionBarrier : DbCommandInterceptor
    {
        internal bool Armed { get; set; }
        internal TaskCompletionSource Selected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("OutboxMessages", StringComparison.Ordinal)
                && command.CommandText.Contains("Cancelled", StringComparison.Ordinal))
            { Armed = false; Selected.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); }
            return result;
        }
    }
}
