using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Una conversación de atención con un número de WhatsApp, de una organización o de Plataforma. Se toma, se pasa a un
/// equipo o a una persona, y la ventana de 24 horas se mide desde el último mensaje de la otra parte.
/// </summary>
public abstract class WhatsAppChatBase : Entity, IVersioned
{
    public const int PreviewLength = 120;
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public uint Version { get; private set; }
    public Guid ChannelId { get; private set; }
    /// <summary>El número de la otra parte en E.164: con él se le responde.</summary>
    public string WaId { get; private set; } = string.Empty;
    public Guid? TeamId { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime LastMessageAtUtc { get; private set; }
    public DateTime? LastInboundAtUtc { get; private set; }
    public string? LastMessagePreview { get; private set; }
    /// <summary>El último mensaje es de la otra parte y nadie lo respondió todavía.</summary>
    public bool AwaitingReply { get; private set; }
    /// <summary>Desde cuándo espera que alguien lo tome; null si está tomado o nunca escribió.</summary>
    public DateTime? WaitingSinceUtc { get; private set; }
    public DateTime? TakenAtUtc { get; private set; }

    public DateTime? WindowOpenUntilUtc => LastInboundAtUtc + Window;
    public bool IsWindowOpen(DateTime nowUtc) => WindowOpenUntilUtc > nowUtc;
    public bool IsTaken => AssigneeUserId is not null;

    protected void Initialize(Guid channelId, PhoneNumber waId, DateTime nowUtc)
    {
        if (channelId == Guid.Empty) throw new ArgumentException("A channel is required.", nameof(channelId));
        ArgumentNullException.ThrowIfNull(waId);
        RequireUtc(nowUtc);
        ChannelId = channelId; WaId = waId.Value; StartedAtUtc = nowUtc; LastMessageAtUtc = nowUtc;
    }

    /// <summary>Un mensaje de la otra parte: abre la ventana y, si nadie lo tomó, queda esperando.</summary>
    public void RecordInbound(DateTime occurredAtUtc, string? preview)
    {
        RequireUtc(occurredAtUtc);
        if (LastInboundAtUtc is null || occurredAtUtc > LastInboundAtUtc) LastInboundAtUtc = occurredAtUtc;
        if (occurredAtUtc >= LastMessageAtUtc) { LastMessageAtUtc = occurredAtUtc; LastMessagePreview = Preview(preview); }
        AwaitingReply = true;
        if (!IsTaken) WaitingSinceUtc ??= occurredAtUtc;
    }

    /// <summary>Un mensaje que salió hacia la otra parte (respuesta, plantilla o bot).</summary>
    public void RecordOutbound(DateTime nowUtc, string? preview)
    {
        RequireUtc(nowUtc);
        LastMessageAtUtc = nowUtc; LastMessagePreview = Preview(preview); AwaitingReply = false;
    }

    /// <summary>Lo toma una persona; si ya lo tomó otra, no cambia.</summary>
    public Result Take(Guid userId, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        RequireUtc(nowUtc);
        if (AssigneeUserId is { } current && current != userId) return Result.Failure(Error.Conflict(WhatsAppErrors.ConversationTakenByOtherCode, "Taken by other."));
        if (AssigneeUserId == userId) return Result.Success();
        AssigneeUserId = userId; TakenAtUtc = nowUtc; WaitingSinceUtc = null;
        return Result.Success();
    }

    /// <summary>Pasa la conversación a un equipo (sin tomar) o a una persona (tomada por ella).</summary>
    public void Transfer(Guid? teamId, Guid? userId, DateTime nowUtc)
    {
        if (teamId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Empty identifiers are invalid.");
        RequireUtc(nowUtc);
        if (teamId is not null) TeamId = teamId;
        AssigneeUserId = userId;
        TakenAtUtc = userId is null ? null : nowUtc;
        WaitingSinceUtc = userId is null ? AwaitingReply ? LastInboundAtUtc ?? nowUtc : null : null;
    }

    /// <summary>El bot la deriva a un equipo; no cambia a quién la tomó.</summary>
    public void RouteToTeam(Guid teamId)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("A team is required.", nameof(teamId));
        TeamId = teamId;
    }

    /// <summary>El equipo se borró: la conversación queda sin equipo.</summary>
    public void LeaveTeam() => TeamId = null;

    private static string? Preview(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length <= PreviewLength ? text : text[..PreviewLength];

    protected static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(value));
    }
}
