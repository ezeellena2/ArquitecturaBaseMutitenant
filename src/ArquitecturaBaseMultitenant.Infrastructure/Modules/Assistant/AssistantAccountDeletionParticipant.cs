using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantAccountDeletionParticipant(ApplicationDbContext db, ITenantContext tenant,
    IAssistantSnapshotRetentionRepository retention, IAssistantUsageFinalizer usage) : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) => Task.FromResult<IReadOnlyList<Error>>([]);
    public async Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct)
    {
        db.RequireTransaction();
        var works = await db.Set<AssistantTurnWork>().Where(row => row.OwnerUserId == context.UserId && (row.Status == "Pending" || row.Status == "Running")).ToArrayAsync(ct);
        foreach (var work in works) work.RequestStop();
    }
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        db.RequireTransaction();
        // Technical recovery locks Work before finance. Deletion must take the same order, even
        // though its outer owner lock already excludes new private admission and checkpoints.
        var works = await db.Set<AssistantTurnWork>().FromSqlInterpolated($"""
            SELECT * FROM platform."AssistantTurnWorks"
            WHERE "OwnerUserId" = {context.UserId} AND "ScopeTenantId" IS NOT DISTINCT FROM {context.TenantId}
            ORDER BY "Id" FOR UPDATE
            """).ToArrayAsync(ct);
        var financeKeys = new List<string> { AdvisoryLockKeys.Account(context.UserId) + ":assistant:finance" };
        if (context.TenantId is { } financeTenant) financeKeys.Add(AdvisoryLockKeys.For(financeTenant, "assistant:finance:budget", financeTenant));
        await db.AcquireAdvisoryLocksAsync(financeKeys, ct);
        var financialOperations = await db.Set<AssistantFinancialOperation>()
            .Where(row => row.UserId == context.UserId && row.ScopeTenantId == context.TenantId).ToArrayAsync(ct);
        var accountingIdentity = Guid.NewGuid();
        foreach (var operation in financialOperations)
        {
            operation.MarkContentDeleted();
            var finalized = await usage.CompleteAsync(new AssistantFinancialBinding(operation.UserId, operation.Access, operation.ScopeTenantId), operation.Id, ct);
            if (finalized.IsFailure) throw new InvalidOperationException("An admitted financial operation could not be closed during deletion.");
            operation.Anonymize(accountingIdentity);
        }
        db.RemoveRange(works);
        // Aggregated accounting keeps an unlinkable identity; daily quotas no longer belong to a live account.
        if (context.TenantId is { } tenantId)
        {
            if (tenant.RequiredTenantId != tenantId) throw new InvalidOperationException("Assistant deletion requires the caller's tenant scope.");
            var conversations = await db.Set<AssistantConversation>().Where(row => row.OwnerUserId == context.UserId && row.TenantId == tenantId).ToArrayAsync(ct);
            foreach (var row in conversations) await AssistantFileCleanupWriter.StageAsync(db, context.UserId, row.Id, false, context.NowUtc, ct);
            db.RemoveRange(conversations);
            await retention.PurgeAccountAsync(context.UserId, ct);
            db.RemoveRange(await db.Set<AssistantUsageFact>().Where(row => row.UserId == context.UserId && row.TenantId == tenantId).ToArrayAsync(ct));
            db.RemoveRange(await db.Set<AssistantOwnKeyDailyQuota>().Where(row => row.UserId == context.UserId && row.TenantId == tenantId).ToArrayAsync(ct));
        }
        else
        {
            if (tenant.TenantId is not null) throw new InvalidOperationException("Global assistant deletion requires the caller's global scope.");
            var conversations = await db.Set<PlatformAssistantConversation>().Where(row => row.OwnerUserId == context.UserId).ToArrayAsync(ct);
            foreach (var row in conversations) await AssistantFileCleanupWriter.StageAsync(db, context.UserId, row.Id, true, context.NowUtc, ct);
            db.RemoveRange(conversations);
            await retention.PurgeAccountAsync(context.UserId, ct);
            db.RemoveRange(await db.Set<AssistantNoticeAcceptance>().Where(row => row.OwnerUserId == context.UserId).ToArrayAsync(ct));
            db.RemoveRange(await db.Set<AssistantPlatformKeyDailyQuota>().Where(row => row.UserId == context.UserId).ToArrayAsync(ct));
        }
    }
}
