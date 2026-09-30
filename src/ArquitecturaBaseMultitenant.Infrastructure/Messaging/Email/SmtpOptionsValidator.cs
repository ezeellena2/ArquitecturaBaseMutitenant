using System.ComponentModel.DataAnnotations;
using MailKit.Security;
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
        Validator.TryValidateObject(options, new ValidationContext(options), results,
            validateAllProperties: true);
        if (options.Security is not (SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect))
        {
            results.Add(new ValidationResult("TLS is required.", [nameof(SmtpOptions.Security)]));
        }

        return results.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(results.SelectMany(result =>
                result.MemberNames.DefaultIfEmpty("Options")
                    .Select(member => $"{SmtpOptions.SectionName}:{member}: {result.ErrorMessage}")));
    }
}
