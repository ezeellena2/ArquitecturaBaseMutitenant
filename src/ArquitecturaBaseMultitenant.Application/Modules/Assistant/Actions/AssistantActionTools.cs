using System.Globalization;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;

/// <summary>Lo sellado del turno que las herramientas de acciones usan: sus acciones, su modelo y si ya hubo propuesta.</summary>
internal sealed record AssistantActionTurn(IReadOnlyList<string> Offered, string ProviderModelId, bool Proposed);

/// <summary>
/// Las tres piezas de IA de las acciones, fijas para todas las áreas (ADR 0068 §7): la lista precargada
/// <c>assistant_actions</c>, <c>read_action</c> y <c>propose_action</c>. Corren en el alcance de solo lectura y no guardan
/// nada. Al proveedor van los tipos de acción, los campos y los nombres de las definiciones; nunca registros ni personas.
/// Lo rechazado vuelve como dato (<see cref="AssistantActionToolData"/>), con la ruta de cada campo, y no corta el turno.
/// </summary>
internal sealed class AssistantActionTools(AssistantActionCatalog catalog)
{
    internal const string ListKey = "assistant_actions";
    internal const string ReadKey = "read_action";
    internal const string ProposeKey = "propose_action";
    /// <summary>La llamada precargada al final del pedido: la lista del turno es un dato, nunca una instrucción.</summary>
    internal const string PreloadCallId = "assistant-actions-preload";
    /// <summary>Con más acciones que estas, la lista precargada trae las áreas y el modelo pide las de una.</summary>
    internal const int PreloadedLimit = 40;
    internal const int MaxProposalBytes = 16 * 1024;
    private const int NameLength = 80;
    private const int TextLength = 100;
    // El destino, una opción o un registro escrito por el modelo: el mismo tope que leen las fuentes.
    private const int TargetLength = AssistantActionLimits.TextLength;

    private static readonly string[] ProposeRequired = ["action", "values"];
    private static readonly string[] ValueRequired = ["field", "value"];

    internal static IReadOnlyList<string> Keys { get; } = [ListKey, ReadKey, ProposeKey];
    internal static bool Handles(string key) => Keys.Contains(key, StringComparer.Ordinal);

    internal static JsonElement Schema(string key) => key switch
    {
        ListKey => JsonSerializer.SerializeToElement(new { type = "object", additionalProperties = false, properties = new { } }),
        ReadKey => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                action = new { type = "string", maxLength = NameLength },
                area = new { type = "string", maxLength = NameLength },
                field = new { type = "string", maxLength = NameLength },
                search = new { type = "string", maxLength = TextLength },
                target = new { type = "string", maxLength = TargetLength },
            },
        }),
        ProposeKey => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            additionalProperties = false,
            required = ProposeRequired,
            properties = new
            {
                action = new { type = "string", maxLength = NameLength },
                target = new { type = "string", maxLength = TargetLength },
                values = new
                {
                    type = "array",
                    maxItems = AssistantTechnicalLimits.MaxProposalFields,
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = ValueRequired,
                        properties = new { field = new { type = "string", maxLength = NameLength }, value = new { } },
                    },
                },
            },
        }),
        _ => throw new InvalidOperationException("The tool is not an action tool."),
    };

    /// <summary>La lista precargada: el id y el nombre corto de lo sellado que se sigue ofreciendo, o las áreas si son muchas.</summary>
    internal async Task<AssistantActionToolData> ListAsync(AssistantActionTurn turn, CultureInfo culture, CancellationToken ct)
    {
        var offered = await catalog.ListSealedAsync(turn.Offered, turn.ProviderModelId, ct);
        return offered.Count > PreloadedLimit
            ? new() { Areas = offered.Select(action => AreaOf(action.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() }
            : new() { Actions = Rows(offered, culture) };
    }

    /// <summary><c>read_action</c>: las acciones de un área, los campos de una acción o los nombres de las opciones de un campo.</summary>
    internal async Task<AssistantActionToolData> ReadAsync(JsonElement arguments, AssistantActionTurn turn, CultureInfo culture,
        CancellationToken ct)
    {
        if (!TryRead(arguments, out var read)) return AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode);
        if (read.Area is { } area)
        {
            var offered = await catalog.ListSealedAsync(turn.Offered, turn.ProviderModelId, ct);
            return new() { Actions = Rows(offered.Where(action => AreaOf(action.Key) == area).ToArray(), culture) };
        }
        var entry = await catalog.FindAsync(read.Action!, turn.Offered, turn.ProviderModelId, ct);
        if (entry is null) return AssistantActionToolData.Reject(AssistantErrors.ActionNotAvailableCode);
        var (fields, rejected) = await FieldsAsync(entry, read.Target, ct);
        if (fields is null) return rejected!;
        if (read.Field is { } name)
        {
            if (fields.SingleOrDefault(field => field.Name == name) is not { Type: AssistantActionFieldType.Choice or AssistantActionFieldType.Choices })
                return AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode, [new(name, AssistantErrors.ActionInvalidCode)]);
            var page = await entry.Source.ListChoicesAsync(new AssistantActionChoiceRequest(entry.Action, name, read.Search, 1, null), ct);
            if (page.IsFailure) return AssistantActionToolData.Reject(AssistantErrors.ActionNotAvailableCode);
            // 0068 §12: 20 opciones por página, aunque la fuente traiga más.
            var choices = page.Value.Items.Take(AssistantActionLimits.ChoicesPerPage).Select(row => row.Name).ToArray();
            return new() { Choices = choices, HasMore = page.Value.HasNext || page.Value.Items.Count > choices.Length };
        }
        return new()
        {
            Action = new(entry.Action.Key, Text(ActionTexts.NameKey(entry.Action), culture), Text(ActionTexts.DescriptionKey(entry.Action), culture),
                entry.Action.Kind.ToString(), entry.Action.Target is not null || entry.Action.FieldsFromTarget,
                fields.Where(field => field.Type != AssistantActionFieldType.Consent).Select(field => Describe(entry.Action, field, culture)).ToArray()),
        };
    }

    /// <summary>
    /// <c>propose_action</c>: pasa por el esquema, la resolución y la validación de la fuente, y no guarda nada. Una por turno:
    /// la segunda vuelve como <c>OnePerTurn</c>.
    /// </summary>
    internal async Task<AssistantActionToolData> ProposeAsync(JsonElement arguments, AssistantActionTurn turn, CancellationToken ct)
    {
        if (!TryPropose(arguments, out var proposal)) return AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode);
        if (turn.Proposed) return AssistantActionToolData.Reject(AssistantErrors.ActionOnePerTurnCode);
        var entry = await catalog.FindAsync(proposal.Action, turn.Offered, turn.ProviderModelId, ct);
        if (entry is null) return AssistantActionToolData.Reject(AssistantErrors.ActionNotAvailableCode);
        var (fields, rejected) = await FieldsAsync(entry, proposal.Target, ct);
        if (fields is null) return rejected!;
        var errors = SchemaErrors(entry.Action, fields, proposal);
        if (errors.Count > 0) return AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode, errors);
        var resolved = await entry.Source.ResolveAsync(new AssistantActionDraft(entry.Action, proposal.Target, proposal.Values), ct);
        if (resolved.IsFailure) return Rejection(resolved.Error);
        if (entry.Action.Requirement is { Kind: AssistantActionRequirementKind.Company, CompanyField: { } companyField }
            && !await HoldsInCompanyAsync(entry.Action, resolved.Value, companyField, ct))
            return AssistantActionToolData.Reject(AssistantErrors.ActionNotAvailableCode);
        var validated = await entry.Source.ValidateAsync(resolved.Value, ct);
        if (validated.IsFailure) return Rejection(validated.Error);
        return new() { Outcome = AssistantActionToolData.Accepted, ProposalId = Guid.CreateVersion7(), Resolution = resolved.Value,
            WrittenTarget = proposal.Target };
    }

    private async Task<bool> HoldsInCompanyAsync(AssistantActionDefinition action, AssistantActionResolution resolution, string companyField,
        CancellationToken ct)
    {
        var company = resolution.Values.SingleOrDefault(value => value.Field == companyField)?.Resolved;
        Guid? companyId = company is { ValueKind: JsonValueKind.String } id && Guid.TryParse(id.GetString(), out var parsed) ? parsed : null;
        return await catalog.HoldsInCompanyAsync(action, companyId, ct);
    }

    // Una regla de la ruta o de la fuente vuelve con su ruta; lo que la fuente todavía no sabe resolver, como no disponible.
    private static AssistantActionToolData Rejection(Error error) => error switch
    {
        ValidationError validation => AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode,
            validation.Errors.SelectMany(field => field.Value.Select(code => new AssistantActionToolError(field.Key, code))).ToArray()),
        { Type: ErrorType.NotImplemented or ErrorType.NotFound } => AssistantActionToolData.Reject(AssistantErrors.ActionNotAvailableCode),
        _ => AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode, [new(string.Empty, error.Code)]),
    };

    // Los campos que salen del destino los da la fuente: sin destino no hay campos, y uno que no resuelve (de otra organización,
    // con varios parecidos) vuelve al modelo con su ruta y su código, como al resolver.
    private static async Task<(IReadOnlyList<AssistantActionField>? Fields, AssistantActionToolData? Rejected)> FieldsAsync(
        AssistantActionEntry entry, string? target, CancellationToken ct)
    {
        if (!entry.Action.FieldsFromTarget) return (entry.Action.Fields, null);
        if (string.IsNullOrWhiteSpace(target)) return (null, AssistantActionToolData.Reject(AssistantErrors.ActionNotAvailableCode));
        var fields = await entry.Source.ReadTargetFieldsAsync(entry.Action, target, ct);
        if (fields.IsSuccess) return (fields.Value, null);
        return (null, fields.Error.Type == ErrorType.NotImplemented
            ? AssistantActionToolData.Reject(AssistantErrors.ActionNotAvailableCode)
            : AssistantActionToolData.Reject(AssistantErrors.ActionInvalidCode, [new("target", fields.Error.Code)]));
    }

    private static List<AssistantActionToolError> SchemaErrors(AssistantActionDefinition action, IReadOnlyList<AssistantActionField> fields,
        ProposeArguments proposal)
    {
        var errors = new List<AssistantActionToolError>();
        var hasTarget = !string.IsNullOrWhiteSpace(proposal.Target);
        // Crear no lleva destino, salvo lo armado como dato; editar y eliminar, sí.
        var targetWrong = action.Target is not null ? !hasTarget : hasTarget && !action.FieldsFromTarget;
        if (targetWrong) errors.Add(new("target", AssistantErrors.ActionInvalidCode));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in proposal.Values)
        {
            var field = fields.SingleOrDefault(item => item.Name == value.Field);
            if (field is null || field.Type == AssistantActionFieldType.Consent || !seen.Add(value.Field) || !Fits(field, value.Value))
                errors.Add(new(value.Field, AssistantErrors.ActionInvalidCode));
        }
        if (action.Kind == AssistantActionKind.Create)
            errors.AddRange(fields.Where(field => field.Required && !seen.Contains(field.Name))
                .Select(field => new AssistantActionToolError(field.Name, AssistantErrors.ActionInvalidCode)));
        return errors;
    }

    // El esquema de cada tipo: lo que el modelo escribe es texto, un número, un sí o no, o una lista con su tope. Resolver y
    // validar quedan en la fuente.
    private static bool Fits(AssistantActionField field, JsonElement value) => field.Type switch
    {
        AssistantActionFieldType.WholeNumber => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var whole) && InRange(field, whole),
        AssistantActionFieldType.DecimalNumber => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && InRange(field, number),
        AssistantActionFieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        AssistantActionFieldType.Money => value.ValueKind is JsonValueKind.Object or JsonValueKind.Number or JsonValueKind.String,
        AssistantActionFieldType.Choices or AssistantActionFieldType.Items =>
            value.ValueKind == JsonValueKind.Array && (field.MaxItems is not { } max || value.GetArrayLength() <= max),
        _ => value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= (field.MaxLength ?? TargetLength),
    };

    private static bool InRange(AssistantActionField field, decimal value) =>
        (field.Minimum is not { } minimum || value >= minimum) && (field.Maximum is not { } maximum || value <= maximum);

    private static AssistantActionToolRow[] Rows(IReadOnlyList<AssistantActionDefinition> actions, CultureInfo culture) =>
        actions.Select(action => new AssistantActionToolRow(action.Key, Text(ActionTexts.NameKey(action), culture))).ToArray();

    private static AssistantActionToolField Describe(AssistantActionDefinition action, AssistantActionField field, CultureInfo culture) =>
        new(field.Name, Text(ActionTexts.FieldKey(action, field.Name), culture), field.Type, field.Required)
        {
            MaxLength = field.MaxLength,
            MaxItems = field.MaxItems,
            Minimum = field.Minimum,
            Maximum = field.Maximum,
            DependsOn = field.DependsOn,
            ItemKey = field.ItemKey,
            ItemFields = field.ItemFields?.Select(item => new AssistantActionToolField(item.Name, item.Name, item.Type, item.Required)
            {
                MaxLength = item.MaxLength,
                Minimum = item.Minimum,
                Maximum = item.Maximum,
            }).ToArray(),
        };

    private static string AreaOf(string key) => key[..key.IndexOf('.', StringComparison.Ordinal)];

    // Las acciones de un módulo leen sus textos de sus recursos; sin texto, va el id, que no es un dato de nadie.
    private static string Text(string key, CultureInfo culture) => ActionTexts.ResourceManager.GetString(key, culture) ?? key;

    private sealed record ReadArguments(string? Action, string? Area, string? Field, string? Search, string? Target);

    private sealed record ProposeArguments(string Action, string? Target, IReadOnlyList<AssistantActionInput> Values);

    private static bool TryRead(JsonElement json, out ReadArguments read)
    {
        read = new(null, null, null, null, null);
        if (json.ValueKind != JsonValueKind.Object) return false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in json.EnumerateObject())
        {
            var limit = property.Name switch { "action" or "area" or "field" => NameLength, "search" => TextLength, "target" => TargetLength, _ => 0 };
            if (limit == 0 || property.Value.ValueKind != JsonValueKind.String || values.ContainsKey(property.Name)) return false;
            var text = property.Value.GetString()!.Trim();
            if (text.Length == 0 || text.Length > limit) return false;
            values[property.Name] = text;
        }
        read = new(values.GetValueOrDefault("action"), values.GetValueOrDefault("area"), values.GetValueOrDefault("field"),
            values.GetValueOrDefault("search"), values.GetValueOrDefault("target"));
        // Una cosa por vez: o un área, o una acción (con su campo o su destino).
        return read.Action is null != (read.Area is null)
            && (read.Area is null || read.Field is null && read.Search is null && read.Target is null);
    }

    private static bool TryPropose(JsonElement json, out ProposeArguments proposal)
    {
        proposal = new(string.Empty, null, []);
        if (json.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(json.GetRawText()) > MaxProposalBytes) return false;
        string? action = null, target = null;
        List<AssistantActionInput>? values = null;
        foreach (var property in json.EnumerateObject())
        {
            switch (property.Name)
            {
                case "action" when action is null && property.Value.ValueKind == JsonValueKind.String:
                    action = property.Value.GetString()!.Trim();
                    break;
                case "target" when target is null && property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                    target = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString()!.Trim();
                    break;
                case "values" when values is null && property.Value.ValueKind == JsonValueKind.Array
                    && property.Value.GetArrayLength() <= AssistantTechnicalLimits.MaxProposalFields:
                    values = [];
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Count() != 2
                            || !item.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String
                            || !item.TryGetProperty("value", out var value)) return false;
                        var name = field.GetString()!.Trim();
                        if (name.Length == 0 || name.Length > NameLength) return false;
                        values.Add(new(name, value.Clone()));
                    }
                    break;
                default:
                    return false;
            }
        }
        if (string.IsNullOrEmpty(action) || action.Length > NameLength || values is null || target is { Length: > TargetLength }) return false;
        proposal = new(action, string.IsNullOrEmpty(target) ? null : target, values);
        return true;
    }
}
