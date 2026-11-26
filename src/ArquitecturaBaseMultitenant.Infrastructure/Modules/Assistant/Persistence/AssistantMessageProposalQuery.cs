using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;

/// <summary>
/// La propuesta de cada mensaje con su estado guardado (ADR 0068 §8): una sola consulta para el historial (el lector de la
/// conversación) y para el mensaje del SSE (el repositorio del turno), así los dos traen lo mismo con el mismo filtro. Lo
/// demás («no disponible», «Sin confirmar») lo calcula el GET de la propuesta. Plataforma no tiene propuestas: no la llama.
/// </summary>
internal static class AssistantMessageProposalQuery
{
    internal static async Task<IReadOnlyDictionary<Guid, AssistantMessageProposalResponse>> ListAsync(ApplicationDbContext db, Guid tenantId,
        Guid conversationId, Guid[] messageIds, CancellationToken ct)
    {
        if (messageIds.Length == 0) return new Dictionary<Guid, AssistantMessageProposalResponse>();
        var rows = await db.Set<AssistantProposal>().AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.ConversationId == conversationId && messageIds.Contains(row.MessageId))
            .Select(row => new { row.MessageId, row.Id, row.ActionId, row.ActionVersion, row.State }).ToArrayAsync(ct);
        return rows.ToDictionary(row => row.MessageId, row => AssistantMessageProposalResponse.From(row.Id, row.ActionId, row.ActionVersion, row.State));
    }
}
