using ArquitecturaBaseMultitenant.Api.Contracts.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Adapta Email/Phone sin parsear teléfonos; ParseEmail conserva el puente pendiente de recuperación T05.</summary>
internal static class AuthContactMapping
{
    public static Result<Email?> Map(string? email, PhoneInputHttpRequest? phone, string? channel)
    {
        if (phone is null)
        {
            if (channel is not null && channel != LoginCodeChannel.Email) return LoginChannelErrors.Unavailable;
            var parsed = AuthEmailMapping.Parse(email, "email");
            return parsed.IsSuccess ? Result.Success<Email?>(parsed.Value) : parsed.Error;
        }
        if (!string.IsNullOrWhiteSpace(email))
            return new ValidationError(new Dictionary<string, string[]> { ["email"] = ["Auth.Contact.ExactlyOne"], ["phone"] = ["Auth.Contact.ExactlyOne"] });
        return Result.Success<Email?>(null);
    }

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
