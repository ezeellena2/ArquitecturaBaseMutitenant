using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Traduce el correo HTTP al value object sin exponerlo en errores o logs.</summary>
internal static class AuthEmailMapping
{
    public static Result<Email> Parse(string? value)
    {
        var email = Email.Create(value);
        if (email.IsSuccess) return email.Value;
        return new ValidationError(new Dictionary<string, string[]>
        {
            ["email"] = [string.IsNullOrWhiteSpace(value) ? ValidationTexts.Required : ValidationTexts.EmailInvalid],
        });
    }
}
