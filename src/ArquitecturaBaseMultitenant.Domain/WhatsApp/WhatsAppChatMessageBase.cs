using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Un mensaje de una conversación de atención: de la otra parte, una respuesta, una plantilla, una nota interna (que nunca
/// sale a Meta) o un evento. Los archivos se informan por su tipo, sin descarga, hasta la E14 (D7).
/// </summary>
public abstract class WhatsAppChatMessageBase : Entity
{
    public Guid ChatId { get; private set; }
    public WhatsAppChatMessageKind Kind { get; private set; }
    public WhatsAppMessageDirection Direction { get; private set; }
    public Guid? AuthorUserId { get; private set; }
    /// <summary>El nombre de quien escribió, copiado al escribir: la conversación no cambia si la persona se va.</summary>
    public string? AuthorName { get; private set; }
    public string? Text { get; private set; }
    public WhatsAppMediaKind? MediaKind { get; private set; }
    public WhatsAppChatMessageStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    /// <summary>El identificador de Meta: en la entrada deduplica, en la salida une los estados de entrega.</summary>
    public string? Wamid { get; private set; }
    public Guid? OutboxMessageId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected void InitializeInbound(Guid chatId, string wamid, string? text, WhatsAppMediaKind? mediaKind, DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wamid);
        Initialize(chatId, text is null && mediaKind is null ? WhatsAppChatMessageKind.Event : mediaKind is null ? WhatsAppChatMessageKind.Text : WhatsAppChatMessageKind.Media,
            WhatsAppMessageDirection.Inbound, null, null, text, occurredAtUtc);
        Wamid = wamid; MediaKind = mediaKind; Status = WhatsAppChatMessageStatus.Received;
    }

    protected void InitializeOutbound(Guid chatId, WhatsAppChatMessageKind kind, Guid? authorUserId, string? authorName, string text,
        Guid outboxMessageId, Guid? templateId, DateTime nowUtc)
    {
        if (kind is not (WhatsAppChatMessageKind.Text or WhatsAppChatMessageKind.Template)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (outboxMessageId == Guid.Empty) throw new ArgumentException("The outbox message is required.", nameof(outboxMessageId));
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Initialize(chatId, kind, WhatsAppMessageDirection.Outbound, authorUserId, authorName, text, nowUtc);
        OutboxMessageId = outboxMessageId; TemplateId = templateId; Status = WhatsAppChatMessageStatus.Pending;
    }

    protected void InitializeNote(Guid chatId, Guid authorUserId, string? authorName, string text, DateTime nowUtc)
    {
        if (authorUserId == Guid.Empty) throw new ArgumentException("An author is required.", nameof(authorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Initialize(chatId, WhatsAppChatMessageKind.Note, WhatsAppMessageDirection.Outbound, authorUserId, authorName, text, nowUtc);
        Status = WhatsAppChatMessageStatus.Sent;
    }

    /// <summary>Lo que Meta informa del envío; nunca vuelve atrás (entregado no pasa a enviado).</summary>
    public bool ApplyDelivery(WhatsAppChatMessageStatus status, string? wamid, string? failureReason)
    {
        if (Direction != WhatsAppMessageDirection.Outbound || Kind == WhatsAppChatMessageKind.Note) return false;
        if (wamid is not null) Wamid ??= wamid;
        if (Rank(status) <= Rank(Status) && status != WhatsAppChatMessageStatus.Failed) return false;
        if (status == WhatsAppChatMessageStatus.Failed && Rank(Status) >= Rank(WhatsAppChatMessageStatus.Delivered)) return false;
        Status = status;
        FailureReason = status == WhatsAppChatMessageStatus.Failed ? failureReason : null;
        return true;
    }

    /// <summary>Borra el texto y el autor (retención y bajas); el mensaje queda como hecho sin contenido.</summary>
    public void ClearContent() { Text = null; AuthorName = null; }

    private void Initialize(Guid chatId, WhatsAppChatMessageKind kind, WhatsAppMessageDirection direction, Guid? authorUserId, string? authorName,
        string? text, DateTime createdAtUtc)
    {
        if (chatId == Guid.Empty) throw new ArgumentException("A chat is required.", nameof(chatId));
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(createdAtUtc));
        if (text?.Length > WhatsAppTextLimits.MessageText) throw new ArgumentException("The text is too long.", nameof(text));
        if (authorName?.Length > TextLimits.PersonDisplayName) authorName = authorName[..TextLimits.PersonDisplayName];
        ChatId = chatId; Kind = kind; Direction = direction; AuthorUserId = authorUserId; AuthorName = authorName; Text = text;
        CreatedAtUtc = createdAtUtc;
    }

    private static int Rank(WhatsAppChatMessageStatus status) => status switch
    {
        WhatsAppChatMessageStatus.Pending => 0,
        WhatsAppChatMessageStatus.Failed => 1,
        WhatsAppChatMessageStatus.Sent => 2,
        WhatsAppChatMessageStatus.Delivered => 3,
        WhatsAppChatMessageStatus.Read => 4,
        _ => -1,
    };
}
