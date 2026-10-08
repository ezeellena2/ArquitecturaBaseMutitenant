using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed partial class WhatsAppOptionsValidator(IOptions<LoginCodeOptions> loginCodes) : IValidateOptions<WhatsAppOptions>
{
    public ValidateOptionsResult Validate(string? name, WhatsAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.PhoneNumberId)) return ValidateOptionsResult.Success;
        var failures = new List<string>();
        if (loginCodes.Value.LifetimeMinutes != 10) failures.Add("WhatsApp Authentication template requires Authentication:LoginCode:LifetimeMinutes=10.");
        if (string.IsNullOrWhiteSpace(options.AccessToken)) failures.Add("Missing WhatsApp:AccessToken.");
        var app = !string.IsNullOrWhiteSpace(options.AppSecret);
        var verify = !string.IsNullOrWhiteSpace(options.VerifyToken);
        if (app && !verify) failures.Add("Missing WhatsApp:VerifyToken; WhatsApp:AppSecret requires both webhook secrets.");
        if (verify && !app) failures.Add("Missing WhatsApp:AppSecret; WhatsApp:VerifyToken requires both webhook secrets.");
        if (options.GraphApiVersion is null || !GraphVersion().IsMatch(options.GraphApiVersion)) failures.Add("WhatsApp:GraphApiVersion must look like v25.0.");
        if (options.AllowedCountries is null || options.AllowedCountries.Length == 0
            || options.AllowedCountries.Any(value => value is null || value.Length != 2 || value.Any(c => c is < 'A' or > 'Z')))
            failures.Add("WhatsApp:AllowedCountries requires ISO alpha-2 country codes.");
        if (options.Templates is null) failures.Add("Missing WhatsApp:Templates.");
        else foreach (var template in WhatsAppTemplateCatalog.Build(options.Templates))
            if (string.IsNullOrWhiteSpace(template.Name) || template.Name.Any(value => !char.IsAsciiLetterLower(value) && !char.IsAsciiDigit(value) && value != '_'))
                failures.Add("Invalid WhatsApp:Templates:" + template.Key + ".");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
    [GeneratedRegex(@"^v\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex GraphVersion();
}
