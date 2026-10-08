using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed partial class WhatsAppOptionsValidator : IValidateOptions<WhatsAppOptions>
{
    public ValidateOptionsResult Validate(string? name, WhatsAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.PhoneNumberId)) return ValidateOptionsResult.Success;
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.AccessToken)) failures.Add("Missing WhatsApp:AccessToken.");
        var app = !string.IsNullOrWhiteSpace(options.AppSecret);
        var verify = !string.IsNullOrWhiteSpace(options.VerifyToken);
        if (app && !verify) failures.Add("Missing WhatsApp:VerifyToken; WhatsApp:AppSecret requires both webhook secrets.");
        if (verify && !app) failures.Add("Missing WhatsApp:AppSecret; WhatsApp:VerifyToken requires both webhook secrets.");
        if (options.GraphApiVersion is null || !GraphVersion().IsMatch(options.GraphApiVersion)) failures.Add("WhatsApp:GraphApiVersion must look like v25.0.");
        if (options.AllowedCountries is null || options.AllowedCountries.Length == 0
            || options.AllowedCountries.Any(value => value is null || value.Length != 2 || value.Any(c => c is < 'A' or > 'Z')))
            failures.Add("WhatsApp:AllowedCountries requires ISO alpha-2 country codes.");
        if (string.IsNullOrWhiteSpace(options.Templates?.LoginCode)) failures.Add("Missing WhatsApp:Templates:LoginCode.");
        if (string.IsNullOrWhiteSpace(options.Templates?.Invitation)) failures.Add("Missing WhatsApp:Templates:Invitation.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
    [GeneratedRegex(@"^v\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex GraphVersion();
}
