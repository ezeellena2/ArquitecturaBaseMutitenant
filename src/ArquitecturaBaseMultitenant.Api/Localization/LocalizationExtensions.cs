using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using Microsoft.AspNetCore.Localization;

namespace ArquitecturaBaseMultitenant.Api.Localization;

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

        options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        options.ApplyCurrentCultureToResponseHeaders = true;
        options.FallBackToParentCultures = false;
        options.FallBackToParentUICultures = false;
        return options;
    }
}
