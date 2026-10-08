using ArquitecturaBaseMultitenant.Api.Contracts.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Puente temporal de T01: correo conserva su conducta y teléfono declara 501 hasta T04.</summary>
internal static class AuthContactMapping
{
    public static Result<Email> ParseEmail(string? email, PhoneInputHttpRequest? phone, string? channel,
        string emailField = "email", string phoneField = "phone")
    {
        if (phone is null)
        {
            if (channel is not null && channel != "email") return LoginChannelErrors.Unavailable;
            return AuthEmailMapping.Parse(email, emailField);
        }
        if (!string.IsNullOrWhiteSpace(email))
            return new ValidationError(new Dictionary<string, string[]>
            {
                [emailField] = ["Auth.Contact.ExactlyOne"],
                [phoneField] = ["Auth.Contact.ExactlyOne"],
            });
        if (string.IsNullOrWhiteSpace(channel))
        {
            // Preview del método anterior no elige transporte.
            if (phoneField == "phone") return LoginChannelErrors.Unavailable;
        }
        return ContractErrors.NotImplemented;
    }
}
