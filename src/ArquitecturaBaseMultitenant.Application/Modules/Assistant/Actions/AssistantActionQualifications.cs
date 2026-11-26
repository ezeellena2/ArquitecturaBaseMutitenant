using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;

/// <summary>
/// Una acción que pasó la calificación contra un modelo (ADR 0068 §12): se ofrece solo con ese modelo, y su presupuesto es el
/// costo medido ahí.
/// </summary>
public sealed record AssistantActionQualification(string Action, int Version, string ProviderModelId, DateOnly QualifiedOn,
    Money MeasuredCost)
{
    [JsonIgnore]
    public string Key => $"{Action}@{Version}";
}

/// <summary>
/// La lista de calificadas, de <c>qualified-actions.json</c>. Está vacía hasta que las acciones pasen la calificación con un
/// modelo real, con el visto bueno del usuario: mientras tanto quedan registradas y no se ofrecen. Los tests cargan la suya.
/// </summary>
internal sealed class AssistantActionQualifications(IReadOnlyList<AssistantActionQualification> rows)
{
    internal const string ResourceName = "ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions.qualified-actions.json";

    internal IReadOnlyList<AssistantActionQualification> Rows { get; } = rows;

    /// <summary>Calificada con algún modelo: se puede ofrecer, si el modelo del turno es uno de ellos.</summary>
    internal bool IsOffered(AssistantActionDefinition action) =>
        Rows.Any(row => string.Equals(row.Key, action.Key, StringComparison.Ordinal));

    internal bool IsQualified(AssistantActionDefinition action, string providerModelId) =>
        Rows.Any(row => string.Equals(row.Key, action.Key, StringComparison.Ordinal)
            && string.Equals(row.ProviderModelId, providerModelId, StringComparison.Ordinal));

    /// <summary>Lee la lista del archivo del módulo; una fila incompleta es un error de quien la cargó, no se ignora.</summary>
    internal static AssistantActionQualifications Load()
    {
        using var stream = typeof(AssistantActionQualifications).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The qualified actions file is missing.");
        return new(Parse(stream));
    }

    internal static IReadOnlyList<AssistantActionQualification> Parse(Stream json)
    {
        var rows = JsonSerializer.Deserialize<AssistantActionQualification[]>(json, AssistantTurnJson.Options)
            ?? throw new InvalidOperationException("The qualified actions file is not a list.");
        foreach (var row in rows)
            if (string.IsNullOrWhiteSpace(row.Action) || row.Version < 1 || string.IsNullOrWhiteSpace(row.ProviderModelId)
                || row.MeasuredCost is null || row.QualifiedOn == default)
                throw new InvalidOperationException("A qualified action needs its action, version, model, date and measured cost.");
        return rows;
    }
}
