using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Application.Resources;

/// <summary>Busca textos de formato según la cadena de idiomas del perfil cultural y falla si tampoco existe el recurso base.</summary>
public static class FormattingTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Formatting", typeof(FormattingTexts).Assembly);

    /// <summary>Etiqueta de la identidad anonimizada, traducida al leer con la cultura de la petición.</summary>
    public static string DeletedAccount => ResourceManager.GetString("DeletedAccount", CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException("Formatting resource DeletedAccount is missing.");

    public static string Get(string key, CultureProfile profile)
    {
        foreach (var language in profile.ResourceLanguages)
        {
            var resourceSet = ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(language),
                createIfNotExists: true, tryParents: false);
            if (resourceSet?.GetString(key) is { } translated)
            {
                return translated;
            }
        }

        return ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Formatting resource {key} is missing for {profile.Entry.Code}.");
    }
}
