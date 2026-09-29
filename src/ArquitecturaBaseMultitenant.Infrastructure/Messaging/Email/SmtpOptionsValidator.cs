using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Las credenciales son obligatorias sólo si el transporte activo es SMTP.</summary>
internal sealed class SmtpOptionsValidator(IOptions<EmailOptions> emailOptions) : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        if (emailOptions.Value.Delivery != EmailDelivery.Smtp)
        {
            return ValidateOptionsResult.Skip;
        }

        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(options, new ValidationContext(options), results,
            validateAllProperties: true)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(results.SelectMany(result =>
                result.MemberNames.DefaultIfEmpty("Options")
                    .Select(member => $"{SmtpOptions.SectionName}:{member}: {result.ErrorMessage}")));
    }
}
