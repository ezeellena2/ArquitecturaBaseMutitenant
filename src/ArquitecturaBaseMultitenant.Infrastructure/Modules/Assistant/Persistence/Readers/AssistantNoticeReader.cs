using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;

internal sealed class AssistantNoticeReader(ApplicationDbContext context) : IAssistantNoticeReader
{
    // Bump only when publishing a reviewed notice. Historical acceptances remain valid only for their revision.
    public string CurrentVersion => "1";

    public Task<bool> ExistsAsync(Guid ownerUserId, string noticeVersion, CancellationToken ct) =>
        context.Set<AssistantNoticeAcceptance>().AsNoTracking()
            .AnyAsync(row => row.OwnerUserId == ownerUserId && row.NoticeVersion == noticeVersion, ct);
}
