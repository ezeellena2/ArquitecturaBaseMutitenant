using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

/// <summary>
/// Las propuestas de las acciones (ADR 0068 §9 y §10), solo de quien pide: leerla con su estado, pedir la clave de cada intento
/// de guardar y avisar que la pantalla guardó, que el servidor comprueba.
/// </summary>
public interface IAssistantProposalService
{
    Task<Result<AssistantProposalResponse>> GetAsync(Guid proposalId, CancellationToken ct);

    Task<Result<AssistantProposalAttemptResponse>> AttemptAsync(Guid proposalId, CancellationToken ct);

    Task<Result<AssistantProposalResponse>> AppliedAsync(Guid proposalId, AssistantProposalAppliedRequest request, CancellationToken ct);
}
