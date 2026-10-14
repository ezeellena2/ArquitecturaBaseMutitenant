using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Un mensaje de una conversación vinculada, copiado para que la persona lo lea en «Mensajes» (D5): solo lo que se dijo
/// con ella desde que vinculó, nunca notas internas ni quién de la organización escribió.
/// </summary>
public sealed class WhatsAppLinkedMessage : Entity, IConsumerBusinessShared
{
    private WhatsAppLinkedMessage() { }

    public Guid ConsumerTenantId { get; private set; }
    public Guid BusinessTenantId { get; private set; }
    public Guid LinkId { get; private set; }
    public WhatsAppMessageDirection Direction { get; private set; }
    public string? Text { get; private set; }
    public WhatsAppMediaKind? MediaKind { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static WhatsAppLinkedMessage Copy(WhatsAppConversationLink link, WhatsAppMessageDirection direction, string? text,
        WhatsAppMediaKind? mediaKind, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(createdAtUtc));
        if (text?.Length > WhatsAppTextLimits.MessageText) throw new ArgumentException("The text is too long.", nameof(text));
        return new WhatsAppLinkedMessage
        {
            ConsumerTenantId = link.ConsumerTenantId, LinkId = link.Id, Direction = direction, Text = text, MediaKind = mediaKind,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>La baja de la persona borra lo que ella escribió; lo que le mandó la organización queda.</summary>
    public void ReplaceConsumerCopy()
    {
        if (Direction == WhatsAppMessageDirection.Inbound) Text = null;
    }
}
