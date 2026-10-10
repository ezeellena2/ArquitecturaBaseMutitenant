using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantNoticeRepository(ApplicationDbContext context) : IAssistantNoticeRepository
{
    public async Task LockAsync(Guid ownerUserId, CancellationToken ct)
    {
        // The owner-phone seed retains Account before the module asks for Legal; keep that cross-domain order.
        await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Account(ownerUserId)], ct);
        await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.LegalDocument(LegalDocumentKind.Terms),
            AdvisoryLockKeys.LegalDocument(LegalDocumentKind.Privacy)], ct);
    }

    public void Add(AssistantNoticeAcceptance acceptance)
    {
        context.RequireTransaction();
        context.Add(acceptance);
    }
}
