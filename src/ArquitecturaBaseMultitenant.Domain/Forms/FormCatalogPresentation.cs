using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Qué arma el título y la descripción de cada opción en WhatsApp (E13), guardado en <see cref="FormCatalog.Presentation"/>: cada uno
/// es una lista de lugares, unidos con « · » al mostrarse. Un lugar es el texto, el código, el valor (si el catálogo es tipado), el
/// detalle o un campo activo (<c>field:&lt;id&gt;</c>). El título siempre tiene uno; la descripción puede no tener ninguno. Lo valida
/// <see cref="FormCatalog.ChangePresentation"/>, y quitar un campo lo saca de los dos (<see cref="WithoutFields"/>).
/// </summary>
public sealed record FormCatalogPresentation(IReadOnlyList<string> Title, IReadOnlyList<string> Description)
{
    public const string Text = "text";
    public const string Code = "code";
    public const string Value = "value";
    public const string Detail = "detail";
    public const string FieldPrefix = "field:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>La de un catálogo nuevo y la de un alta de la versión anterior (0060): el texto en el título y el detalle debajo.</summary>
    public static FormCatalogPresentation Default { get; } = new([Text], [Detail]);

    /// <summary>El lugar de un campo: <c>field:</c> y su identidad.</summary>
    public static string Field(Guid fieldId) => FieldPrefix + fieldId.ToString("D");

    /// <summary>La identidad del campo de un lugar <c>field:&lt;id&gt;</c>; <c>null</c> si es otro lugar o no tiene una identidad.</summary>
    public static Guid? FieldIdOf(string place) =>
        place is not null && place.StartsWith(FieldPrefix, StringComparison.Ordinal) && Guid.TryParse(place.AsSpan(FieldPrefix.Length), out var id)
            && id != Guid.Empty
            ? id
            : null;

    /// <summary>
    /// La misma sin los lugares de los campos quitados: si el título se queda sin ninguno, vuelve al texto (E13, «Presentación»).
    /// </summary>
    public FormCatalogPresentation WithoutFields(IReadOnlySet<Guid> removedFieldIds)
    {
        ArgumentNullException.ThrowIfNull(removedFieldIds);
        bool Keeps(string place) => FieldIdOf(place) is not { } id || !removedFieldIds.Contains(id);
        var title = Title.Where(Keeps).ToList();
        return new FormCatalogPresentation(title.Count == 0 ? [Text] : title, Description.Where(Keeps).ToList());
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static FormCatalogPresentation FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<FormCatalogPresentation>(json, JsonOptions)
            ?? throw new InvalidOperationException("The catalog presentation is empty.");
    }
}
