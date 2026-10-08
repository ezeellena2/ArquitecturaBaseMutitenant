using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>El consentimiento se guarda junto a la identidad al verificar el código.</summary>
public sealed record VerifySignupRequest(Email? Email, string? Code, bool AcceptedTerms,
    string? Culture = null, string? TimeZoneId = null, PhoneInputRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(VerifySignupRequest);
}
