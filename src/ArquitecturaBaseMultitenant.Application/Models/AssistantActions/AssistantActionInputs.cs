using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

/// <summary>
/// Lo que comparten las fuentes de acciones para leer lo que escribió el modelo (ADR 0068 §5 y §7), en un solo lugar: una
/// corrección vale para todas, y la próxima área lo usa en vez de copiarlo. Elegir por nombre (igual gana, parecido también,
/// con la duda), leer un elemento de un <c>Choices</c>, aplicar sus operaciones a lo que hay y paginar un catálogo cerrado.
/// </summary>
internal static class AssistantActionInputs
{
    internal const string Add = "add";
    internal const string Remove = "remove";

    /// <summary>Cómo se guarda lo resuelto: sin la duda cuando no hay.</summary>
    internal static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Si un texto se puede resolver: escrito y dentro de <see cref="AssistantActionLimits.TextLength"/>.</summary>
    internal static bool IsWritten([NotNullWhen(true)] string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= AssistantActionLimits.TextLength;

    /// <summary>El texto como búsqueda del lector (<c>ApplySearch</c>): sin espacios a los lados y dentro de su tope.</summary>
    internal static string SearchOf(string text)
    {
        var trimmed = text.Trim();
        return trimmed[..Math.Min(trimmed.Length, PagedRequest.MaxSearchLength)];
    }

    /// <summary>
    /// Igual gana; parecido (lo contiene) también. La duda: varios parecidos, o ninguno igual. Sin parecidos, null.
    /// </summary>
    internal static (T Item, string? Doubt)? Match<T>(IReadOnlyList<T> candidates, Func<T, IEnumerable<string>> names, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return null;
        var similar = candidates.Where(item => names(item).Any(name => Contains(name, trimmed)))
            .OrderBy(item => names(item).First(), StringComparer.CurrentCulture).ToArray();
        if (similar.Length == 0) return null;
        var exact = similar.Where(item => names(item).Any(name => Same(name, trimmed))).ToArray();
        var chosen = exact.Length > 0 ? exact[0] : similar[0];
        return (chosen, exact.Length == 1 && similar.Length == 1 ? null : AssistantActionDoubts.Similar);
    }

    /// <summary>Iguales sin mirar mayúsculas ni acentos.</summary>
    internal static bool Same(string candidate, string text) =>
        CultureInfo.InvariantCulture.CompareInfo.Compare(candidate.Trim(), text.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

    /// <summary>El candidato contiene el texto, sin mirar mayúsculas ni acentos.</summary>
    internal static bool Contains(string candidate, string text) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(candidate, text.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    /// <summary>
    /// Un elemento de un <c>Choices</c>: un nombre (sumar) o <c>{op: "add" | "remove", value}</c>, con el texto escrito y
    /// dentro del tope.
    /// </summary>
    internal static bool TryReadOp(JsonElement element, out string op, out string text)
    {
        (op, text) = (Add, string.Empty);
        if (element.ValueKind == JsonValueKind.String) text = element.GetString()!;
        else if (element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Count() == 2
            && element.TryGetProperty("op", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() is Add or Remove
            && element.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            (op, text) = (kind.GetString()!, value.GetString()!);
        else return false;
        return IsWritten(text);
    }

    /// <summary>Lo que queda después de sumar y quitar sobre lo que hay.</summary>
    internal static HashSet<TId> Apply<TId>(IEnumerable<TId> current, IEnumerable<AssistantActionOp<TId>> ops)
    {
        var result = current.ToHashSet();
        foreach (var op in ops)
            if (op.Op == Remove) result.Remove(op.Id);
            else result.Add(op.Id);
        return result;
    }

    /// <summary>Las operaciones resueltas de un <c>Choices</c>, como las guarda la propuesta.</summary>
    internal static AssistantActionOp<TId>[] OpsOf<TId>(JsonElement? resolved) =>
        resolved is { ValueKind: JsonValueKind.Array } array ? array.Deserialize<AssistantActionOp<TId>[]>(Json) ?? [] : [];

    /// <summary>Las operaciones resueltas de un campo de la propuesta.</summary>
    internal static AssistantActionOp<TId>[] OpsOf<TId>(AssistantActionResolution resolution, string field) =>
        [.. resolution.Values.Where(value => value.Field == field).SelectMany(value => OpsOf<TId>(value.Resolved))];

    /// <summary>Lo resuelto de un campo de la propuesta, o null si no lo propone.</summary>
    internal static JsonElement? ResolvedOf(AssistantActionResolution resolution, string field) =>
        resolution.Values.SingleOrDefault(value => value.Field == field)?.Resolved;

    /// <summary>El id resuelto de una opción o un registro.</summary>
    internal static Guid? IdOf(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } id && Guid.TryParse(id.GetString(), out var parsed) ? parsed : null;

    /// <summary>Lo rechazado que vuelve al modelo, con la ruta del campo y su código.</summary>
    internal static ValidationError Invalid(string path, string code) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { [path] = [code] });

    /// <summary>
    /// Una página de un catálogo cerrado (los alcances, los permisos del catálogo): no es una tabla, así que se busca y se
    /// pagina en memoria, de a <see cref="AssistantActionLimits.ChoicesPerPage"/>. Lo que sale de la base pagina en su lector.
    /// </summary>
    internal static PagedResult<AssistantActionChoiceRow> PageOf(IEnumerable<AssistantActionChoiceRow> catalog, string? search, int page)
    {
        var found = catalog.Where(row => string.IsNullOrWhiteSpace(search) || Contains(row.Name, search)).ToArray();
        var current = Math.Max(PagedRequest.DefaultPage, page);
        return new PagedResult<AssistantActionChoiceRow>(
            [.. found.Skip((current - 1) * AssistantActionLimits.ChoicesPerPage).Take(AssistantActionLimits.ChoicesPerPage)],
            current, AssistantActionLimits.ChoicesPerPage, found.Length);
    }
}

/// <summary>Un elemento resuelto de un <c>Choices</c>: sumar o quitar, el id (un código o un Guid) y su duda.</summary>
internal sealed record AssistantActionOp<TId>(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("id")] TId Id,
    [property: JsonPropertyName("doubt")] string? Doubt);
