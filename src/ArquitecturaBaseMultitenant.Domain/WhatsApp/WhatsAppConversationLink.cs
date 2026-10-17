using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Una conversación de la organización vinculada con la cuenta de quien escribe (ADR 0038, D2): un dato compartido. La
/// persona la ve en «Mensajes» desde que la vinculó (D5) y puede desvincularla; la organización ve que está vinculada y el
/// nombre de la cuenta. Se crea del lado de la organización, cuando la persona toca el botón del bot.
/// </summary>
public sealed class WhatsAppConversationLink : Entity, IConsumerBusinessShared, IAuditable
{
    private WhatsAppConversationLink() { }

    public Guid ConsumerTenantId { get; private set; }
    public Guid BusinessTenantId { get; private set; }
    public Guid ChatId { get; private set; }
    /// <summary>La cuenta dueña del teléfono al vincular: un número reciclado no le copia nada a otra (D5).</summary>
    public Guid AccountId { get; private set; }
    public string OrganizationName { get; private set; } = string.Empty;
    /// <summary>El número de la organización en E.164, para «Seguir en WhatsApp».</summary>
    public string DisplayNumber { get; private set; } = string.Empty;
    [NotAudited]
    public string? ConsumerName { get; private set; }
    public DateTime LinkedAtUtc { get; private set; }
    [NotAudited]
    public DateTime? LastMessageAtUtc { get; private set; }
    [NotAudited]
    public string? LastMessagePreview { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    /// <summary>La organización la sella el guardado; la persona es su espacio personal.</summary>
    public static WhatsAppConversationLink Create(Guid consumerTenantId, Guid chatId, Guid accountId, string? consumerName,
        string organizationName, string displayNumber, DateTime nowUtc)
    {
        if (consumerTenantId == Guid.Empty || chatId == Guid.Empty || accountId == Guid.Empty) throw new ArgumentException("Empty identifiers.");
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayNumber);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        return new WhatsAppConversationLink
        {
            ConsumerTenantId = consumerTenantId, ChatId = chatId, AccountId = accountId,
            ConsumerName = consumerName?.Length > TextLimits.PersonDisplayName ? consumerName[..TextLimits.PersonDisplayName] : consumerName,
            OrganizationName = organizationName.Length > TextLimits.OrganizationName ? organizationName[..TextLimits.OrganizationName] : organizationName,
            DisplayNumber = displayNumber, LinkedAtUtc = nowUtc,
        };
    }

    public void RecordMessage(DateTime atUtc, string? preview)
    {
        if (atUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(atUtc));
        if (LastMessageAtUtc is { } last && atUtc < last) return;
        LastMessageAtUtc = atUtc;
        LastMessagePreview = string.IsNullOrWhiteSpace(preview) ? null
            : preview.Length <= WhatsAppChatBase.PreviewLength ? preview : preview[..WhatsAppChatBase.PreviewLength];
    }

    public void ReplaceConsumerCopy() => ConsumerName = null;
}
