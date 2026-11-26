using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>
/// La propuesta de un turno (ADR 0068 §8): la acción sellada con su versión, lo que escribió el modelo con el id resuelto y la
/// duda de cada campo, el destino y la versión que tenía al proponer. Va con el mensaje del asistente, privada y con RLS, y se
/// borra con él. Nunca guarda nombres resueltos. «No disponible» y «Sin confirmar» no se guardan: se calculan al leer.
/// </summary>
public sealed class AssistantProposal : Entity, ITenantOwned, IVersioned
{
    public const string ActionKind = "Action";
    public const int ActionIdLength = 80;
    public const int AppliedLocationLength = 2048;
    /// <summary>Las claves de los intentos de guardar que se conservan; la más vieja sale primero.</summary>
    public const int MaxAttemptKeys = 10;

    private AssistantProposal() { }
    private AssistantProposal(Guid id) : base(id) { }

    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Kind { get; private set; } = ActionKind;
    public string ActionId { get; private set; } = string.Empty;
    public int ActionVersion { get; private set; }
    /// <summary>JSON: lo que escribió el modelo (el destino y cada campo), el id resuelto y la duda; nunca un nombre.</summary>
    public string Values { get; private set; } = "{}";
    public Guid? TargetId { get; private set; }
    public uint? BaseVersion { get; private set; }
    public AssistantProposalStatus State { get; private set; }
    /// <summary>JSON: las claves de los intentos de guardar, con su fecha.</summary>
    public string AttemptKeys { get; private set; } = "[]";
    public DateTime? AppliedAtUtc { get; private set; }
    public string? AppliedLocation { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>El id con versión de la acción, como <c>users.invite@1</c>.</summary>
    public string ActionKey => $"{ActionId}@{ActionVersion}";

    public static AssistantProposal Create(Guid id, Guid conversationId, Guid messageId, Guid ownerUserId, string actionId, int actionVersion,
        string values, Guid? targetId, uint? baseVersion, DateTime nowUtc)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A conversation is required.", nameof(conversationId));
        if (messageId == Guid.Empty) throw new ArgumentException("A message is required.", nameof(messageId));
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (actionId.Length > ActionIdLength) throw new ArgumentException("The action id is too long.", nameof(actionId));
        ArgumentOutOfRangeException.ThrowIfLessThan(actionVersion, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(values);
        if (targetId == Guid.Empty) throw new ArgumentException("An empty target is not a target.", nameof(targetId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new AssistantProposal(id)
        {
            ConversationId = conversationId, MessageId = messageId, OwnerUserId = ownerUserId, ActionId = actionId,
            ActionVersion = actionVersion, Values = values, TargetId = targetId, BaseVersion = baseVersion,
            State = AssistantProposalStatus.New, CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>Otra propuesta posterior de su clase, en su conversación, la reemplaza: ya no se abre (0068 §8).</summary>
    public void Replace()
    {
        if (State == AssistantProposalStatus.New) State = AssistantProposalStatus.Replaced;
    }

    /// <summary>La pantalla guardó y el servidor lo comprobó: con el enlace a lo hecho, si lo hay.</summary>
    public void MarkApplied(string? location, DateTime nowUtc)
    {
        if (State == AssistantProposalStatus.Applied) return;
        if (State != AssistantProposalStatus.New) throw new InvalidOperationException("Only a new proposal can be applied.");
        if (location is { Length: > AppliedLocationLength }) throw new ArgumentException("The location is too long.", nameof(location));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        (State, AppliedLocation, AppliedAtUtc) = (AssistantProposalStatus.Applied, location, nowUtc);
    }

    /// <summary>Las claves de los intentos de guardar, de la más vieja a la más nueva.</summary>
    public IReadOnlyList<AssistantProposalAttemptKey> ReadAttemptKeys() =>
        JsonSerializer.Deserialize<AssistantProposalAttemptKey[]>(AttemptKeys) ?? [];

    /// <summary>Suma la clave de un intento nuevo; con el tope lleno, sale la más vieja.</summary>
    public void AddAttemptKey(Guid key, DateTime nowUtc)
    {
        if (key == Guid.Empty) throw new ArgumentException("An attempt key cannot be empty.", nameof(key));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        if (State != AssistantProposalStatus.New) throw new InvalidOperationException("Only a new proposal takes attempts.");
        var keys = ReadAttemptKeys().Append(new AssistantProposalAttemptKey(key, nowUtc)).TakeLast(MaxAttemptKeys).ToArray();
        AttemptKeys = JsonSerializer.Serialize(keys);
    }
}

/// <summary>El estado guardado de una propuesta; «no disponible» y «Sin confirmar» se calculan al leer.</summary>
public enum AssistantProposalStatus
{
    New,
    Replaced,
    Applied,
}

/// <summary>La clave de un intento de guardar desde una propuesta, con su fecha.</summary>
public sealed record AssistantProposalAttemptKey(Guid Key, DateTime CreatedAtUtc);
