using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Un contacto de la organización: un número que le escribió o que se importó, con sus etiquetas y si acepta promociones,
/// con el origen y la fecha de ese dato (ADR 0038). Sin «Sí» queda «Sin respuesta» y no recibe difusiones.
/// </summary>
public sealed class WhatsAppCustomer : Entity, ITenantOwned, IVersioned
{
    private readonly List<string> tags = [];
    private WhatsAppCustomer() { }
    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public string WaId { get; private set; } = string.Empty;
    public string? Name { get; private set; }
    public IReadOnlyCollection<string> Tags => tags.AsReadOnly();
    public WhatsAppConsent Consent { get; private set; } = WhatsAppConsent.NoAnswer;
    public WhatsAppConsentSource? ConsentSource { get; private set; }
    public DateTime? ConsentChangedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastConversationAtUtc { get; private set; }
    /// <summary>Cuándo el bot le preguntó por promociones: lo hace una sola vez por contacto.</summary>
    public DateTime? PromotionsAskedAtUtc { get; private set; }
    /// <summary>Cuándo el bot le ofreció vincular la conversación con su cuenta (D2): una sola vez por contacto.</summary>
    public DateTime? LinkOfferedAtUtc { get; private set; }

    public static WhatsAppCustomer Create(PhoneNumber waId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(waId);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        return new WhatsAppCustomer { WaId = waId.Value, CreatedAtUtc = nowUtc };
    }

    public void Rename(string? name)
    {
        if (name?.Length > TextLimits.PersonDisplayName) throw new ArgumentException("The name is too long.", nameof(name));
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    public void ReplaceTags(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var next = values.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (next.Length > WhatsAppFeature.MaxContactTags || next.Any(value => value.Length > WhatsAppTextLimits.Tag))
            throw new ArgumentException("Too many or too long tags.", nameof(values));
        tags.Clear();
        tags.AddRange(next);
    }

    public void AddTag(string tag) => ReplaceTags([.. tags, tag]);

    /// <summary>Cambia el consentimiento con su origen; repetir el mismo valor no cambia la fecha.</summary>
    public bool ChangeConsent(WhatsAppConsent consent, WhatsAppConsentSource source, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        if (Consent == consent && ConsentSource is not null) return false;
        Consent = consent; ConsentSource = source; ConsentChangedAtUtc = nowUtc;
        return true;
    }

    /// <summary>
    /// Una fila de la importación: completa el nombre y suma las etiquetas (hasta el tope); con «Sí» acepta promociones, salvo
    /// que se haya dado de baja; sin «Sí», un contacto sin dato queda «Sin respuesta» con origen Import.
    /// </summary>
    public void Import(string? name, IEnumerable<string> tags, bool acceptsPromotions, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (!string.IsNullOrWhiteSpace(name)) Rename(name);
        ReplaceTags([.. this.tags.Concat(tags.Select(value => value.Trim())).Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(WhatsAppFeature.MaxContactTags)]);
        if (acceptsPromotions) { if (Consent != WhatsAppConsent.Unsubscribed) ChangeConsent(WhatsAppConsent.Accepted, WhatsAppConsentSource.Import, nowUtc); }
        else if (ConsentSource is null) ChangeConsent(WhatsAppConsent.NoAnswer, WhatsAppConsentSource.Import, nowUtc);
    }

    public void MarkLinkOffered(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        LinkOfferedAtUtc ??= nowUtc;
    }

    public void MarkPromotionsAsked(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        PromotionsAskedAtUtc ??= nowUtc;
    }

    public void RecordConversation(DateTime occurredAtUtc)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(occurredAtUtc));
        if (LastConversationAtUtc is null || occurredAtUtc > LastConversationAtUtc) LastConversationAtUtc = occurredAtUtc;
    }
}
