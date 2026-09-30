using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using Microsoft.AspNetCore.Localization;

namespace ArquitecturaBaseMultitenant.Api.Localization;

/// <summary>Limita Accept-Language a las culturas habilitadas del catálogo y define su fallback.</summary>
internal static class LocalizationExtensions
{
    /// <summary>La cultura de la petición sale solo de Accept-Language y de las filas habilitadas del catálogo.</summary>
    public static RequestLocalizationOptions CreateRequestLocalizationOptions(this SupportedCultures supportedCultures)
    {
        ArgumentNullException.ThrowIfNull(supportedCultures);

        var codes = supportedCultures.Codes.ToArray();
        var options = new RequestLocalizationOptions();
        options.SetDefaultCulture(supportedCultures.DefaultCulture)
            .AddSupportedCultures(codes)
            .AddSupportedUICultures(codes);

        var headerProvider = new AcceptLanguageHeaderRequestCultureProvider();
        options.RequestCultureProviders =
        [
            new CustomRequestCultureProvider(async context =>
            {
                var requested = await headerProvider.DetermineProviderCultureResult(context);
                if (requested is null)
                {
                    return null;
                }

                foreach (var candidate in requested.Cultures)
                {
                    if (string.IsNullOrWhiteSpace(candidate.Value))
                    {
                        continue;
                    }

                    var language = candidate.Value;
                    var match = codes.FirstOrDefault(code =>
                        string.Equals(code, language, StringComparison.OrdinalIgnoreCase));
                    match ??= codes.FirstOrDefault(code =>
                        string.Equals(code.Split('-')[0], language.Split('-')[0], StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        return new ProviderCultureResult(match);
                    }
                }

                return null;
            }),
        ];
        options.ApplyCurrentCultureToResponseHeaders = true;
        options.FallBackToParentCultures = false;
        options.FallBackToParentUICultures = false;
        return options;
    }
}
