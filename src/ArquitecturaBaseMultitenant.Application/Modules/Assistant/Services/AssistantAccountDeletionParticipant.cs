using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantAccountDeletionParticipant(IAssistantAccountDeletionCleanupRepository cleanup, ITenantContext tenant,
    AssistantSnapshotRetentionWriter retention, IAssistantUsageFinalizer usage) : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) => Task.FromResult<IReadOnlyList<Error>>([]);
    public async Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct)
    {
        cleanup.RequireTransaction();
        foreach (var work in await cleanup.ListActiveWorksAsync(context.UserId, ct)) work.RequestStop();
    }
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        cleanup.RequireTransaction();
        // Technical recovery locks Work before finance; deletion keeps the same order.
        var works = await cleanup.LockWorksAsync(context.UserId, context.TenantId, ct);
        await cleanup.LockFinanceAsync(context.UserId, context.TenantId, ct);
        var financialOperations = await cleanup.ListFinancialOperationsAsync(context.UserId, context.TenantId, ct);
        var accountingIdentity = Guid.NewGuid();
        foreach (var operation in financialOperations)
        {
            operation.MarkContentDeleted();
            var finalized = await usage.CompleteAsync(new AssistantFinancialBinding(operation.UserId, operation.Access, operation.ScopeTenantId), operation.Id, ct);
            if (finalized.IsFailure) throw new InvalidOperationException("An admitted financial operation could not be closed during deletion.");
            operation.Anonymize(accountingIdentity);
        }
        cleanup.RemoveWorks(works);
        if (context.TenantId is { } tenantId)
        {
            if (tenant.RequiredTenantId != tenantId) throw new InvalidOperationException("Assistant deletion requires the caller's tenant scope.");
            await cleanup.RemovePrivateContentAsync(context.UserId, tenantId, context.NowUtc, ct);
        }
        else
        {
            if (tenant.TenantId is not null) throw new InvalidOperationException("Global assistant deletion requires the caller's global scope.");
            await cleanup.RemoveGlobalContentAsync(context.UserId, context.NowUtc, ct);
        }
        await retention.PurgeAccountAsync(context.UserId, ct);
    }
}
