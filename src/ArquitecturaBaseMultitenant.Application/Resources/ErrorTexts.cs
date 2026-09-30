using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Resources;

/// <summary>Resuelve títulos y mensajes de error desde los recursos de la cultura activa; conserva la clave si falta una traducción.</summary>
public static class ErrorTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Errors", typeof(ErrorTexts).Assembly);

    public static string? Find(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture);

    public static string Get(string key) => Find(key) ?? key;

    public static string Title(ErrorType type) => Get("Title." + Enum.GetName(type));
}
