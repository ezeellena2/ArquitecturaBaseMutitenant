using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantModelRepository(ApplicationDbContext context) : IAssistantModelRepository
{
    public async Task<AssistantModelPrice?> GetAsync(Guid id, uint expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var row = await context.Set<AssistantModelPrice>().SingleOrDefaultAsync(value => value.Id == id, ct);
        if (row is not null) context.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
        return row;
    }
    public Task<bool> ExistsAsync(string provider, string providerModelId, Guid? exceptId, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<AssistantModelPrice>().AnyAsync(value => value.Provider == provider && value.ProviderModelId == providerModelId && value.Id != exceptId, ct);
    }
    public void Add(AssistantModelPrice row) { context.RequireTransaction(); context.Add(row); }
    public void MarkUpdated(AssistantModelPrice row) { context.RequireTransaction(); context.Entry(row).State = EntityState.Modified; }
}
