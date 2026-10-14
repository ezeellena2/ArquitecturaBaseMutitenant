using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Una difusión de la organización (ADR 0038): una plantilla Marketing aprobada a los contactos que aceptan promociones,
/// todos o los de una etiqueta. Se programa o sale enseguida; se puede cancelar solo mientras está programada. Guarda la
/// plantilla, sus valores, la cantidad de destinatarios y el costo estimado con que se creó, para mostrarla y duplicarla.
/// </summary>
public sealed class WhatsAppBroadcast : Entity, ITenantOwned, IVersioned, IAuditable
{
    private List<string> _variables = [];

    private WhatsAppBroadcast() { }

    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid TemplateId { get; private set; }
    public string TemplateName { get; private set; } = string.Empty;
    public IReadOnlyList<string> Variables => _variables.AsReadOnly();
    public WhatsAppAudienceKind AudienceKind { get; private set; }
    public string? AudienceTag { get; private set; }
    public WhatsAppBroadcastStatus Status { get; private set; }
    public DateTime? ScheduledAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public int Recipients { get; private set; }
    public decimal EstimatedCostAmount { get; private set; }
    public string EstimatedCostCurrency { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    public Money EstimatedCost => new(EstimatedCostAmount, CurrencyCode.Create(EstimatedCostCurrency).Value);

    public static WhatsAppBroadcast Create(string name, Guid templateId, string templateName, IEnumerable<string> variables, WhatsAppAudienceKind kind,
        string? tag, DateTime? scheduledAtUtc, int recipients, Money estimatedCost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(estimatedCost);
        if (name.Trim().Length > TextLimits.ShortName) throw new ArgumentException("The name is too long.", nameof(name));
        if (templateId == Guid.Empty) throw new ArgumentException("A template is required.", nameof(templateId));
        if (!Enum.IsDefined(kind) || (kind == WhatsAppAudienceKind.Tag) != tag is { Length: > 0 }) throw new ArgumentException("Invalid audience.", nameof(tag));
        if (scheduledAtUtc is { Kind: not DateTimeKind.Utc }) throw new ArgumentException("UTC required.", nameof(scheduledAtUtc));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recipients);
        var values = variables.ToList();
        if (values.Count > WhatsAppFeature.MaxTemplateVariables || values.Any(value => value is not { Length: > 0 and <= WhatsAppTextLimits.TemplateVariable }))
            throw new ArgumentException("Invalid variables.", nameof(variables));
        return new WhatsAppBroadcast
        {
            Name = name.Trim(), TemplateId = templateId, TemplateName = templateName, _variables = values, AudienceKind = kind, AudienceTag = tag?.Trim(),
            Status = WhatsAppBroadcastStatus.Scheduled, ScheduledAtUtc = scheduledAtUtc, Recipients = recipients,
            EstimatedCostAmount = estimatedCost.Amount, EstimatedCostCurrency = estimatedCost.Currency.Value,
        };
    }

    /// <summary>Si ya le toca salir: sin fecha, enseguida; programada, cuando llega su hora; o si ya empezó.</summary>
    public bool IsDue(DateTime nowUtc) => Status == WhatsAppBroadcastStatus.Sending
        || Status == WhatsAppBroadcastStatus.Scheduled && (ScheduledAtUtc is null || ScheduledAtUtc <= nowUtc);

    public void Start(DateTime nowUtc)
    {
        if (Status != WhatsAppBroadcastStatus.Scheduled) return;
        Status = WhatsAppBroadcastStatus.Sending; StartedAtUtc = nowUtc;
    }

    public void Finish(DateTime nowUtc)
    {
        if (Status is WhatsAppBroadcastStatus.Sent or WhatsAppBroadcastStatus.Cancelled) return;
        Status = WhatsAppBroadcastStatus.Sent; StartedAtUtc ??= nowUtc; SentAtUtc = nowUtc;
    }

    public Result Cancel()
    {
        if (Status != WhatsAppBroadcastStatus.Scheduled) return Result.Failure(WhatsAppErrors.BroadcastNotCancellable);
        Status = WhatsAppBroadcastStatus.Cancelled;
        return Result.Success();
    }
}

/// <summary>
/// Un destinatario de una difusión. Pending hasta que sale; Sent cuando se encoló (entregado, leído o fallido se leen del
/// envío); Replied si escribió después; Unsubscribed si tocó «No recibir más» o si al salir ya no aceptaba promociones;
/// Failed si no se pudo mandar.
/// </summary>
public sealed class WhatsAppBroadcastRecipient : Entity, ITenantOwned
{
    private WhatsAppBroadcastRecipient() { }

    public Guid TenantId { get; private set; }
    public Guid BroadcastId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string WaId { get; private set; } = string.Empty;
    public WhatsAppRecipientStatus Status { get; private set; }
    public Guid? OutboxMessageId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    public static WhatsAppBroadcastRecipient Create(Guid broadcastId, Guid customerId, PhoneNumber waId, DateTime nowUtc) => new()
    {
        BroadcastId = broadcastId, CustomerId = customerId, WaId = waId.Value, Status = WhatsAppRecipientStatus.Pending, ChangedAtUtc = nowUtc,
    };

    public void MarkSent(Guid outboxMessageId, DateTime nowUtc)
    {
        if (Status != WhatsAppRecipientStatus.Pending) return;
        Status = WhatsAppRecipientStatus.Sent; OutboxMessageId = outboxMessageId; SentAtUtc = nowUtc; ChangedAtUtc = nowUtc;
    }

    public void MarkReplied(DateTime nowUtc)
    {
        if (Status != WhatsAppRecipientStatus.Sent) return;
        Status = WhatsAppRecipientStatus.Replied; ChangedAtUtc = nowUtc;
    }

    public void MarkUnsubscribed(DateTime nowUtc)
    {
        if (Status is WhatsAppRecipientStatus.Unsubscribed or WhatsAppRecipientStatus.Failed) return;
        Status = WhatsAppRecipientStatus.Unsubscribed; ChangedAtUtc = nowUtc;
    }

    public void Fail(string reason, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != WhatsAppRecipientStatus.Pending) return;
        Status = WhatsAppRecipientStatus.Failed; FailureReason = reason.Length > TextLimits.ShortName ? reason[..TextLimits.ShortName] : reason; ChangedAtUtc = nowUtc;
    }
}
