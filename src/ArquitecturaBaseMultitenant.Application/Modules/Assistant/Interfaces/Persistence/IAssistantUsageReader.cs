using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantUsageReader
{
    Task<AssistantUsageReadPage> ListOrganizationAsync(DateOnly month, AssistantUsageGrouping grouping,
        int page, int pageSize, DateTime nowUtc, CancellationToken cancellationToken);
    Task<AssistantUsageReadPage> ListPlatformAsync(DateOnly month, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AssistantPersonDailyUsageRow>> ListDailyAsync(IReadOnlyCollection<Guid> userIds,
        AssistantMode mode, DateOnly dayUtc, CancellationToken cancellationToken);
}
