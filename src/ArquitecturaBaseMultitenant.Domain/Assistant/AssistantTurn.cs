using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public sealed class AssistantTurn : AssistantTurnBase, ITenantOwned
{
    private AssistantTurn() { }
    public Guid TenantId { get; private set; }
    /// <summary>
    /// Las acciones que ofreció el turno, selladas (0068 §6): un arreglo JSON de ids con versión, como <c>users.invite@1</c>.
    /// Solo se proponen esas, y cada llamada vuelve a autorizar. Nulo si el turno no ofreció ninguna.
    /// </summary>
    public string? OfferedActions { get; private set; }
    public const int OfferedActionKeyLength = 80;

    /// <summary>Sella las acciones del turno una sola vez, al aceptarlo; una lista vacía no sella nada.</summary>
    public void OfferActions(IReadOnlyCollection<string> keys)
    {
        if (OfferedActions is not null) throw new InvalidOperationException("The offered actions are sealed once.");
        if (keys.Any(key => string.IsNullOrWhiteSpace(key) || key.Length > OfferedActionKeyLength))
            throw new ArgumentException("An offered action is an id with its version.", nameof(keys));
        if (keys.Count > 0) OfferedActions = System.Text.Json.JsonSerializer.Serialize(keys.Distinct(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Los ids con versión que selló el turno; vacío si no ofreció ninguna.</summary>
    public IReadOnlyList<string> ReadOfferedActions() => OfferedActions is null ? []
        : System.Text.Json.JsonSerializer.Deserialize<string[]>(OfferedActions) ?? [];

    public static AssistantTurn Create(Guid conversationId, DateTime nowUtc)
    {
        var row = new AssistantTurn();
        row.Initialize(conversationId, nowUtc);
        return row;
    }
}
