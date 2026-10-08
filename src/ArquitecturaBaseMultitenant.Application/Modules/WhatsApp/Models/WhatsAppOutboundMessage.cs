using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>Payload transitorio; se serializa exclusivamente en el outbox cifrado.</summary>
public sealed record WhatsAppOutboundMessage(string PhoneNumberId, string To, string TemplateName,
    string Language, IReadOnlyList<string> BodyParameters, string? ButtonParameter = null,
    bool IsAuthentication = false)
{
    public WhatsAppMessageKind Kind => IsAuthentication ? WhatsAppMessageKind.Authentication : WhatsAppMessageKind.Template;
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PhoneNumberId);
        if (PhoneNumberId.Length > ArquitecturaBaseMultitenant.Domain.Common.TextLimits.ShortName) throw new ArgumentException("Invalid technical number identifier.");
        if (PhoneNumber.Create(To).IsFailure) throw new ArgumentException("Canonical phone required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(TemplateName);
        if (Language is not ("es" or "en")) throw new ArgumentException("Unsupported template language.");
        if (IsAuthentication && (BodyParameters.Count != 1 || ButtonParameter != BodyParameters[0]))
            throw new ArgumentException("Authentication body and copy button must contain the same code.");
    }
    public override string ToString() => nameof(WhatsAppOutboundMessage);
}
