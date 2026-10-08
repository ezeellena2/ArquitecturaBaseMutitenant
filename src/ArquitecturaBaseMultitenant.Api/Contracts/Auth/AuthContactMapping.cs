using ArquitecturaBaseMultitenant.Api.Contracts.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Adapta Email/Phone sin parsear teléfonos; la normalización y el canal pertenecen al servicio.</summary>
internal static class AuthContactMapping
{
    public static Result<Email?> Map(string? email, PhoneInputHttpRequest? phone, string? channel,
        string emailField = "email", string phoneField = "phone")
    {
        if (phone is null)
        {
            if (channel is not null && channel != LoginCodeChannel.Email) return LoginChannelErrors.Unavailable;
            var parsed = AuthEmailMapping.Parse(email, emailField);
            return parsed.IsSuccess ? Result.Success<Email?>(parsed.Value) : parsed.Error;
        }
        if (!string.IsNullOrWhiteSpace(email))
            return new ValidationError(new Dictionary<string, string[]> { [emailField] = ["Auth.Contact.ExactlyOne"], [phoneField] = ["Auth.Contact.ExactlyOne"] });
        return Result.Success<Email?>(null);
    }

}
