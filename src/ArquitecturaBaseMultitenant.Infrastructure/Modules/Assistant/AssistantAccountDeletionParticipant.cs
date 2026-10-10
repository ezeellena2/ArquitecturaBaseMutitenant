using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantAccountDeletionParticipant(ApplicationDbContext db, ITenantContext tenant) : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) => Task.FromResult<IReadOnlyList<Error>>([]);
    public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        db.RequireTransaction();
        if (context.TenantId is { } tenantId)
        {
            if (tenant.RequiredTenantId != tenantId) throw new InvalidOperationException("Assistant deletion requires the caller's tenant scope.");
            var conversations = await db.Set<AssistantConversation>().Where(row => row.OwnerUserId == context.UserId && row.TenantId == tenantId).ToArrayAsync(ct);
            foreach (var row in conversations) await AssistantFileCleanupWriter.StageAsync(db, context.UserId, row.Id, false, context.NowUtc, ct);
            db.RemoveRange(conversations);
            db.RemoveRange(await db.Set<AssistantDurableOperation>().Where(row => row.OwnerUserId == context.UserId && row.TenantId == tenantId).ToArrayAsync(ct));
        }
        else
        {
            if (tenant.TenantId is not null) throw new InvalidOperationException("Global assistant deletion requires the caller's global scope.");
            var conversations = await db.Set<PlatformAssistantConversation>().Where(row => row.OwnerUserId == context.UserId).ToArrayAsync(ct);
            foreach (var row in conversations) await AssistantFileCleanupWriter.StageAsync(db, context.UserId, row.Id, true, context.NowUtc, ct);
            db.RemoveRange(conversations);
            db.RemoveRange(await db.Set<PlatformAssistantDurableOperation>().Where(row => row.OwnerUserId == context.UserId).ToArrayAsync(ct));
            db.RemoveRange(await db.Set<AssistantNoticeAcceptance>().Where(row => row.OwnerUserId == context.UserId).ToArrayAsync(ct));
        }
    }
}
