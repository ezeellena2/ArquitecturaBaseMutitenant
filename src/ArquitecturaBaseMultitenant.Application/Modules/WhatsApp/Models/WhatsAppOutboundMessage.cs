using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>Payload transitorio; se serializa exclusivamente en el outbox cifrado.</summary>
public sealed record WhatsAppOutboundMessage(string PhoneNumberId, string To, string TemplateName,
    string Language, IReadOnlyList<string> BodyParameters, string? ButtonParameter = null,
    bool IsAuthentication = false, string? Body = null, string? Footer = null,
    string? LinkLabel = null, string? LinkUrl = null, IReadOnlyList<WhatsAppReplyButton>? ReplyButtons = null,
    DateTime? ReplyExpiresAtUtc = null)
{
    public WhatsAppMessageKind Kind => Body is not null
        ? LinkUrl is not null || ReplyButtons?.Count > 0 ? WhatsAppMessageKind.Interactive : WhatsAppMessageKind.Text
        : IsAuthentication ? WhatsAppMessageKind.Authentication : WhatsAppMessageKind.Template;
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PhoneNumberId);
        if (PhoneNumberId.Length > ArquitecturaBaseMultitenant.Domain.Common.TextLimits.ShortName) throw new ArgumentException("Invalid technical number identifier.");
        if (PhoneNumber.Create(To).IsFailure) throw new ArgumentException("Canonical phone required.");
        if (Body is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Body);
            if (ReplyExpiresAtUtc is not { Kind: DateTimeKind.Utc }) throw new ArgumentException("UTC reply expiry required.");
            if (IsAuthentication || LinkUrl is not null && ReplyButtons?.Count > 0) throw new ArgumentException("Invalid reply shape.");
            if (LinkUrl is not null && (!Uri.TryCreate(LinkUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(LinkLabel))) throw new ArgumentException("HTTPS CTA required.");
            if (ReplyButtons is { Count: > 3 }) throw new ArgumentException("At most three reply buttons.");
            return;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(TemplateName);
        if (Language is not ("es" or "en")) throw new ArgumentException("Unsupported template language.");
        if (IsAuthentication && (BodyParameters.Count != 1 || ButtonParameter != BodyParameters[0]))
            throw new ArgumentException("Authentication body and copy button must contain the same code.");
    }
    public override string ToString() => nameof(WhatsAppOutboundMessage);
}
public sealed record WhatsAppReplyButton(string Id, string Title);
