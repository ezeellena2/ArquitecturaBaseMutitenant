using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Los filtros de un catálogo de miembros o de contactos (§3, C5), que se guardan como jsonb con <c>format: 1</c>. Las columnas y
/// los operadores son textos de la fuente y se validan contra ella al guardar; <see cref="SampleId"/> es la persona o el cliente de
/// ejemplo, que se guarda para no pedirlo en cada guardado.
/// </summary>
public sealed record FormCatalogFilters(int SourceVersion, IReadOnlyList<FormCatalogCondition> Conditions, bool ExcludeRecipient,
    string? Detail, FormCatalogFilterSort? Sort, Guid? SampleId)
{
    public const int CurrentFormat = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>La forma del jsonb; un cambio de forma se lee un despliegue antes de escribirse (ADR 0060).</summary>
    [JsonPropertyOrder(-1)]
    public int Format { get; init; } = CurrentFormat;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static FormCatalogFilters FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<FormCatalogFilters>(json, JsonOptions)
            ?? throw new InvalidOperationException("The catalog filters are empty.");
    }
}

/// <summary>Una condición, unida a las demás con «y»: <c>{column, operator, values[]}</c> (C5).</summary>
public sealed record FormCatalogCondition(string Column, string Operator, IReadOnlyList<string> Values);

/// <summary>El orden de las opciones de una fuente: una columna que la fuente deja ordenar.</summary>
public sealed record FormCatalogFilterSort(string Column, bool Descending);
