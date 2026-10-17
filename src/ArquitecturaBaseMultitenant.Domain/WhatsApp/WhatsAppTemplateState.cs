using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Lo que Meta dice de una plantilla (nombre e idioma) en una cuenta de WhatsApp: estado, categoría y motivo del rechazo.
/// Es global, como los números: lo escriben el webhook y la sincronización sin entrar a una organización, y cada una lee
/// solo el de las cuentas de sus números. El motivo es el código de Meta (INVALID_FORMAT); nunca un texto libre.
/// </summary>
public sealed class WhatsAppTemplateState : Entity
{
    private WhatsAppTemplateState() { }

    public string WabaId { get; private set; } = string.Empty;
    public string MetaName { get; private set; } = string.Empty;
    public string MetaLanguage { get; private set; } = string.Empty;
    public string? MetaTemplateId { get; private set; }
    public WhatsAppTemplateStatus Status { get; private set; }
    public WhatsAppTemplateCategory Category { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static WhatsAppTemplateState Create(string wabaId, string metaName, string metaLanguage, WhatsAppTemplateCategory category, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wabaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(metaLanguage);
        return new WhatsAppTemplateState
        {
            WabaId = wabaId, MetaName = metaName, MetaLanguage = WhatsAppTemplateBase.ToMetaLanguage(metaLanguage), Category = category,
            Status = WhatsAppTemplateStatus.Pending, UpdatedAtUtc = nowUtc,
        };
    }

    /// <summary>Aplica lo que vino de Meta; lo que no vino queda como estaba.</summary>
    public void Apply(string? metaTemplateId, WhatsAppTemplateStatus? status, WhatsAppTemplateCategory? category, string? rejectionReason,
        DateTime nowUtc)
    {
        if (metaTemplateId is { Length: > 0 and <= WhatsAppTextLimits.MetaIdentifier }) MetaTemplateId = metaTemplateId;
        if (status is { } next)
        {
            Status = next;
            RejectionReason = next == WhatsAppTemplateStatus.Rejected && rejectionReason is { Length: > 0 and <= TextLimits.ShortName }
                ? rejectionReason : null;
        }
        if (category is { } kind) Category = kind;
        UpdatedAtUtc = nowUtc;
    }
}
