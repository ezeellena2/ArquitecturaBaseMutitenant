using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Un campo de las opciones de un catálogo a mano (E13), guardado en <see cref="FormCatalog.FieldsJson"/> junto con los quitados,
/// que conservan sus valores y su clave. La identidad no cambia; la clave (<see cref="Key"/>) nace del nombre, se escribe hasta
/// guardar y después queda fija (130), y es la que guardan los valores de cada opción. <see cref="Min"/> y <see cref="Max"/> son
/// texto canónico (C6): un número con punto o una fecha <c>yyyy-MM-dd</c>. Lo arma y lo valida <see cref="FormCatalog.ChangeFields"/>.
/// </summary>
public sealed record FormCatalogField(Guid Id, string Key, string Name, FormCatalogFieldType Type, Guid? SourceCatalogId,
    bool Required, bool Unique, int? MaxLength, string? Min, string? Max, int Position, bool IsActive)
{
    /// <summary>Campos activos de un catálogo, igual para todas las organizaciones (42 y 105): quitar uno libera su lugar.</summary>
    public const int MaxActive = 20;
    /// <summary>Campos guardados, entre activos y quitados: cinco veces los activos, así un catálogo no crece sin fin.</summary>
    public const int MaxStored = 5 * MaxActive;
    /// <summary>La clave de un nombre que no empieza con una letra: «2024 · Ventas» da <c>campo_2024_ventas</c>.</summary>
    private const string KeyFallback = "campo";

    /// <summary>Los lugares de la presentación que no pueden ser la clave de un campo: una clave así suma un número.</summary>
    public static IReadOnlyList<string> ReservedKeys { get; } =
        [FormCatalogPresentation.Text, FormCatalogPresentation.Code, FormCatalogPresentation.Value, FormCatalogPresentation.Detail];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Si es una clave posible: <c>^[a-z][a-z0-9_]{0,39}$</c>.</summary>
    public static bool IsValidKey(string? key) =>
        key is { Length: > 0 and <= FormsTextLimits.FieldKey }
        && char.IsAsciiLetterLower(key[0])
        && key.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_');

    /// <summary>
    /// La clave que nace del nombre: en minúsculas, sin acentos y con <c>_</c> en lugar de lo demás, hasta 40 caracteres. Un nombre
    /// sin letras ni números da <c>campo</c>, y uno que empieza con un número, <c>campo_</c> delante.
    /// </summary>
    public static string KeyFromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder(name.Length);
        foreach (var character in name.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(character);
            if (char.IsAsciiLetterLower(lower) || char.IsAsciiDigit(lower)) builder.Append(lower);
            else if (builder.Length > 0 && builder[^1] != '_') builder.Append('_');
        }

        var key = builder.ToString().Trim('_');
        if (key.Length == 0) return KeyFallback;
        if (!char.IsAsciiLetterLower(key[0])) key = KeyFallback + "_" + key;
        return Fit(key, string.Empty);
    }

    /// <summary>
    /// La clave libre: la misma, o con <c>_2</c>, <c>_3</c>… si choca con un lugar de la presentación o con una que ya se usa (la de
    /// un campo quitado), sin pasar de 40 caracteres.
    /// </summary>
    internal static string FreeKey(string key, IReadOnlySet<string> taken)
    {
        if (!ReservedKeys.Contains(key) && !taken.Contains(key)) return key;
        for (var number = 2; ; number++)
        {
            var candidate = Fit(key, "_" + number.ToString(CultureInfo.InvariantCulture));
            if (!ReservedKeys.Contains(candidate) && !taken.Contains(candidate)) return candidate;
        }
    }

    public static string ToJson(IEnumerable<FormCatalogField> fields) => JsonSerializer.Serialize(fields, JsonOptions);

    public static IReadOnlyList<FormCatalogField> ListFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<List<FormCatalogField>>(json, JsonOptions)
            ?? throw new InvalidOperationException("The catalog fields are empty.");
    }

    private static string Fit(string key, string suffix)
    {
        var head = key[..Math.Min(key.Length, FormsTextLimits.FieldKey - suffix.Length)].TrimEnd('_');
        return head + suffix;
    }
}

/// <summary>
/// Un campo como llega a <see cref="FormCatalog.ChangeFields"/>: uno guardado con <see cref="Id"/> o uno nuevo sin él. La clave puede
/// faltar: en uno guardado queda la suya, y en uno nuevo nace del nombre. <see cref="Min"/> y <see cref="Max"/> van como texto
/// canónico; vacíos, sin límite.
/// </summary>
public sealed record FormCatalogFieldDraft(Guid? Id, string? Key, string Name, FormCatalogFieldType Type, Guid? SourceCatalogId,
    bool Required, bool Unique, int? MaxLength, string? Min, string? Max);
