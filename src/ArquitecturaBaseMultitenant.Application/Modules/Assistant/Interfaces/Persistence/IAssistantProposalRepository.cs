using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

/// <summary>
/// Las propuestas de las acciones (ADR 0068 §8), en la organización del claim y con RLS: cada lectura es de su dueño, en una
/// conversación suya y de su acceso. La de otra persona u organización no existe.
/// </summary>
public interface IAssistantProposalRepository
{
    /// <summary>La propuesta de su dueño, sin seguirla; null si no existe o es de otra persona, acceso u organización.</summary>
    Task<AssistantProposal?> FindAsync(Guid owner, Access access, Guid id, CancellationToken ct);

    /// <summary>La propuesta de su dueño, bloqueada para cambiarla en la transacción abierta.</summary>
    Task<AssistantProposal?> LockAsync(Guid owner, Access access, Guid id, CancellationToken ct);

    /// <summary>Las personas que nombra el mensaje de la propuesta (sus sujetos), para ver si alguna cuenta se dio de baja.</summary>
    Task<IReadOnlyList<Guid>> ListSubjectsAsync(AssistantProposal proposal, CancellationToken ct);

    /// <summary>Las propuestas de unos mensajes de una conversación del dueño, sin seguirlas.</summary>
    Task<IReadOnlyList<AssistantProposal>> ListByMessagesAsync(Guid owner, Access access, Guid conversationId, IReadOnlyList<Guid> messageIds,
        CancellationToken ct);

    /// <summary>Si el mensaje ya tiene su propuesta: una por turno (0068 §8).</summary>
    Task<bool> ExistsForMessageAsync(Guid conversationId, Guid messageId, CancellationToken ct);

    /// <summary>
    /// Las propuestas nuevas de una clase en la conversación, tomadas con lock (<c>FOR NO KEY UPDATE</c>) y leídas después,
    /// seguidas, para reemplazarlas: un intento de guardarla que la tiene termina antes y no se pisa (guardado.md).
    /// </summary>
    Task<IReadOnlyList<AssistantProposal>> LockNewAsync(Guid conversationId, string kind, CancellationToken ct);

    void Add(AssistantProposal proposal);
}
