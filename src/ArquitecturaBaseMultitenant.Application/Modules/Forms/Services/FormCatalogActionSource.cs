using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// La acción de las opciones de un catálogo a mano (ADR 0068, «Consecuencias»; §17 de la E12): declarada una vez por tipo, con
/// los campos que salen del catálogo destino (<see cref="AssistantActionDefinition.FieldsFromTarget"/>). Solo lee, con el lector
/// de catálogos, y valida armando el mismo pedido que guarda el editor (<c>PUT</c>), con su validador y las reglas de cada opción.
/// <list type="bullet">
/// <item>El destino es un catálogo a mano por su nombre; con varios parecidos vuelve al modelo con
/// <see cref="FormCatalogErrors.AmbiguousCode"/>, y uno dado de baja, que el editor no deja cambiar, con su código.</item>
/// <item>Las opciones son un <c>Items</c> con clave <c>code</c>: sumar, cambiar y quitar, como en el editor. Una opción guardada
/// la nombra el modelo por su código o su texto, y la resuelve el lector con su búsqueda; su id nunca va al proveedor.</item>
/// <item>Quitar la marca para borrar al guardar, como la pantalla; si algo la usa, lo dice la ruta (<c>ItemInUse</c>). No
/// propone el orden ni importar.</item>
/// </list>
/// Elegir por nombre y leer una fila es de <see cref="AssistantActionInputs"/>, igual para todas las fuentes.
/// </summary>
internal sealed class FormCatalogActionSource(
    IFormCatalogReader catalogs,
    IEnumerable<IValidator<UpdateFormCatalogRequest>> updateValidators) : IAssistantActionSource
{
    internal const string OptionsField = "options";
    internal const string CodeField = "code";
    internal const string TextField = "text";
    internal const string DetailField = "detail";
    internal const string ActiveField = "active";
    internal const string ValueField = "value";

    private const string TargetPath = "target";
    private const string DateFormat = "yyyy-MM-dd";

    public static IReadOnlyList<AssistantActionDefinition> Actions { get; } =
    [
        new()
        {
            Id = "catalogoptions.edit",
            Version = 1,
            Kind = AssistantActionKind.Edit,
            Requirement = AssistantActionRequirement.Organization(Permissions.Forms.CatalogsManage),
            Module = "forms",
            SaveRoute = "PUT api/organization/forms/catalogs/{id}",
            Confirmation = AssistantActionConfirmation.Page,
            Screen = AssistantActionScreen.CatalogEditor,
            Target = new("GET api/organization/forms/catalogs/{id}"),
            FieldsFromTarget = true,
        },
    ];

    public IReadOnlyList<AssistantActionDefinition> Definitions => Actions;

    // Ningún campo es un Choice ni un Choices: las opciones se nombran como texto.
    public Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(Result.Failure<PagedResult<AssistantActionChoiceRow>>(Invalid(request.Field, ValidationError.ErrorCode)));
    }

    /// <summary>Las columnas del editor para ese catálogo: Código, Texto, Detalle, Estado y, en uno tipado, el Valor de su tipo.</summary>
    public async Task<Result<IReadOnlyList<AssistantActionField>>> ReadTargetFieldsAsync(AssistantActionDefinition action, string target,
        CancellationToken cancellationToken)
    {
        var found = await MatchTargetAsync(target, cancellationToken);
        return found.IsFailure ? found.Error : Result.Success<IReadOnlyList<AssistantActionField>>(FieldsOf(found.Value.ValueType));
    }

    public async Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var found = await MatchTargetAsync(draft.Target, cancellationToken);
        if (found.IsFailure) return Invalid(TargetPath, found.Error.Code);
        var catalog = found.Value;

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var values = new List<AssistantActionResolvedValue>();
        foreach (var input in draft.Values)
        {
            if (input.Field != OptionsField || input.Value.ValueKind != JsonValueKind.Array)
            {
                errors[input.Field] = [ValidationError.ErrorCode];
                continue;
            }
            var ops = new List<OptionOp>();
            var index = 0;
            foreach (var element in input.Value.EnumerateArray())
                if (await ResolveOptionAsync(catalog, element, $"{OptionsField}[{index++}]", ops, errors, cancellationToken) is { } op) ops.Add(op);
            var doubt = ops.Any(op => op.Doubt is not null) ? AssistantActionDoubts.Similar : null;
            values.Add(new(OptionsField, input.Value, JsonSerializer.SerializeToElement(ops, AssistantActionInputs.Json), doubt));
        }

        if (errors.Count > 0) return new ValidationError(errors);
        return new AssistantActionResolution(draft.Action, catalog.Id, catalog.Version, values);
    }

    /// <summary>
    /// Vuelve a leer por id el catálogo y cada opción guardada que nombra: un catálogo que ya no es a mano y activo, o una opción
    /// por cambiar que ya no está, es <c>NotFound</c>; una por quitar que ya no está no cambia nada y no se muestra (ya se guardó,
    /// o la borró otra persona). Cada fila va como la dibuja el editor: <c>{op, key, row?}</c>, con el id de la opción como clave
    /// (el código, si es nueva) y su texto de ahora como nombre.
    /// </summary>
    public async Task<Result<AssistantActionReview>> ReviewAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (await FindTargetAsync(resolution.TargetId, cancellationToken) is not { } catalog) return FormCatalogErrors.NotFound;
        var ops = OpsOf(resolution);
        var saved = await SavedAsync(catalog.Id, ops, cancellationToken);
        if (ops.Any(op => op.Id is { } id && op.Op != AssistantActionInputs.Remove && !saved.ContainsKey(id)))
            return FormCatalogErrors.ItemNotFound(OptionsField);

        var views = ops.Where(op => op.Id is not { } id || saved.ContainsKey(id)).Select(op => op.Id is { } id
            ? new AssistantActionValueView(OptionsField, JsonSerializer.SerializeToElement(new RowView(op.Op, id.ToString("D"), RowOf(op.Row, catalog.ValueType)),
                AssistantActionInputs.Json), saved[id].Text, op.Doubt)
            : new AssistantActionValueView(OptionsField, JsonSerializer.SerializeToElement(new RowView(op.Op, op.Row!.Code!, RowOf(op.Row, catalog.ValueType)),
                AssistantActionInputs.Json), null, null));
        return new AssistantActionReview(new(catalog.Id, catalog.Name, catalog.Version), [.. views]);
    }

    /// <summary>
    /// Arma el pedido del editor con lo resuelto (lo nuevo y lo cambiado en <c>items</c>, con lo que cada opción tiene ahora, y lo
    /// quitado en <c>deleteItems</c>), corre su validador, las reglas de cada opción en memoria, el tope y el código único entre lo
    /// propuesto y lo guardado que queda, sin guardar.
    /// </summary>
    public async Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (await FindTargetAsync(resolution.TargetId, cancellationToken) is not { } catalog) return Invalid(TargetPath, FormCatalogErrors.NotFoundCode);
        var ops = OpsOf(resolution);
        var saved = await SavedAsync(catalog.Id, ops, cancellationToken);
        if (ops.Any(op => op.Id is { } id && !saved.ContainsKey(id))) return Invalid(OptionsField, FormCatalogErrors.ItemNotFoundCode);

        var items = new List<(int Index, FormCatalogItemInput Input)>();
        for (var index = 0; index < ops.Length; index++)
        {
            if (ops[index] is not { Row: { } row } op) continue;
            var before = op.Id is { } id ? saved[id] : null;
            items.Add((index, new FormCatalogItemInput(before?.Id, before is null ? $"n{index}" : null, row.Code ?? before?.Code,
                row.Text ?? before?.Text, row.Detail ?? before?.Detail, row.Value ?? before?.Value, row.Active ?? before?.IsActive ?? true)));
        }
        Guid[] deleted = [.. ops.Where(op => op.Op == AssistantActionInputs.Remove).Select(op => op.Id!.Value)];
        var request = new UpdateFormCatalogRequest(catalog.Id, catalog.Name, catalog.Description, catalog.IsActive,
            resolution.BaseVersion ?? catalog.Version, [.. items.Select(item => item.Input)], null, null, deleted, null, null, null);
        var failure = await RequestValidator.ValidateAsync(request, updateValidators, cancellationToken);
        if (failure is not null) return AssistantActionInputs.OnActionFields(failure, FieldOfProperty);

        // Las reglas de cada opción que corre la ruta (el largo y el valor según el tipo), en memoria.
        var draft = FormCatalog.CreateManual(catalog.Name, catalog.Description, catalog.ValueType);
        foreach (var (index, input) in items)
        {
            var item = FormCatalogItem.Create(draft, index, input.Code!, input.Text!, input.Detail, input.Value, input.IsActive!.Value, 0);
            if (item.IsFailure) return Invalid($"{OptionsField}[{index}].{FieldOfCode(item.Error.Code)}", item.Error.Code);
        }
        var added = items.Count(item => item.Input.ItemId is null);
        if (catalog.TotalItems + added - deleted.Length > FormCatalogLimits.MaxItems) return Invalid(OptionsField, FormCatalogErrors.TooManyItemsCode);
        return await CheckCodesAsync(catalog.Id, items, deleted, cancellationToken);
    }

    private async Task<OptionOp?> ResolveOptionAsync(FormCatalogSummary catalog, JsonElement element, string path, List<OptionOp> resolved,
        Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        if (!AssistantActionInputs.TryReadRowOp(element, out var op, out var key, out var written))
        {
            errors[path] = [ValidationError.ErrorCode];
            return null;
        }

        Guid? itemId = null;
        string? doubt = null;
        if (key is not null)
        {
            var page = await catalogs.ListItemsAsync(catalog.Id, new ListFormCatalogItemsRequest
            {
                Search = AssistantActionInputs.SearchOf(key),
                PageSize = AssistantActionLimits.Candidates,
            }, cancellationToken);
            if (AssistantActionInputs.Match(page.Items, OptionNames, key) is not { } match)
            {
                errors[path] = [FormCatalogErrors.ItemNotFoundCode];
                return null;
            }
            // Dos operaciones sobre la misma opción no se dibujan: la pantalla aplica una por fila.
            if (resolved.Any(other => other.Id == match.Item.Id))
            {
                errors[path] = [ValidationError.ErrorCode];
                return null;
            }
            (itemId, doubt) = (match.Item.Id, match.Doubt);
        }

        OptionRow? row = null;
        if (written is { } fields && (row = ReadRow(catalog.ValueType, fields, path, op == AssistantActionInputs.Add, errors)) is null) return null;
        return new OptionOp(op, itemId, row, doubt);
    }

    // Lo que escribió el modelo en una fila, con los límites y el tipo de cada columna; el valor queda como texto canónico (C6).
    private static OptionRow? ReadRow(FormCatalogValueType valueType, JsonElement fields, string path, bool added,
        Dictionary<string, string[]> errors)
    {
        var failed = false;
        void Fail(string field, string code)
        {
            errors[$"{path}.{field}"] = [code];
            failed = true;
        }

        string? code = null, text = null, detail = null, value = null;
        bool? active = null;
        foreach (var property in fields.EnumerateObject())
        {
            var cell = property.Value;
            switch (property.Name)
            {
                case CodeField:
                    code = cell.ValueKind == JsonValueKind.String ? cell.GetString()!.Trim() : null;
                    if (code is not { Length: > 0 and <= FormsTextLimits.OptionCode }) Fail(CodeField, ValidationError.ErrorCode);
                    break;
                case TextField:
                    text = cell.ValueKind == JsonValueKind.String ? cell.GetString()!.Trim() : null;
                    if (string.IsNullOrEmpty(text)) Fail(TextField, ValidationError.ErrorCode);
                    else if (text.Length > FormsTextLimits.OptionText) Fail(TextField, FormCatalogErrors.ItemTextTooLongCode);
                    break;
                case DetailField:
                    // Vacío borra el detalle, como dejarlo en blanco en la pantalla.
                    detail = cell.ValueKind == JsonValueKind.String ? cell.GetString()!.Trim() : null;
                    if (detail is null) Fail(DetailField, ValidationError.ErrorCode);
                    else if (detail.Length > FormsTextLimits.OptionDetail) Fail(DetailField, FormCatalogErrors.ItemDetailTooLongCode);
                    break;
                case ActiveField:
                    active = cell.ValueKind is JsonValueKind.True or JsonValueKind.False ? cell.GetBoolean() : null;
                    if (active is null) Fail(ActiveField, ValidationError.ErrorCode);
                    break;
                case ValueField:
                    // Uno de texto no tiene valor: el editor no muestra la columna.
                    value = valueType == FormCatalogValueType.Text ? null : CanonicalValue(valueType, cell);
                    if (value is null) Fail(ValueField, valueType == FormCatalogValueType.Text ? ValidationError.ErrorCode : FormCatalogErrors.ValueInvalidCode);
                    break;
                default:
                    // Una columna que el editor no tiene.
                    errors[path] = [ValidationError.ErrorCode];
                    failed = true;
                    break;
            }
        }

        if (added)
        {
            if (code is null) Fail(CodeField, ValidationError.ErrorCode);
            if (text is null) Fail(TextField, ValidationError.ErrorCode);
            if (value is null && valueType != FormCatalogValueType.Text) Fail(ValueField, FormCatalogErrors.ValueRequiredCode);
        }
        else if (code is null && text is null && detail is null && active is null && value is null)
        {
            errors[path] = [ValidationError.ErrorCode];
            failed = true;
        }
        return failed ? null : new OptionRow(code, text, detail, active, value);
    }

    private static string? CanonicalValue(FormCatalogValueType valueType, JsonElement cell) => valueType switch
    {
        FormCatalogValueType.WholeNumber when cell.ValueKind == JsonValueKind.Number && cell.TryGetDecimal(out var whole)
            && whole == decimal.Truncate(whole) => FormCatalogValueText.Number(whole),
        FormCatalogValueType.DecimalNumber when cell.ValueKind == JsonValueKind.Number && cell.TryGetDecimal(out var number)
            => FormCatalogValueText.Number(number),
        FormCatalogValueType.Date when cell.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(cell.GetString(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            => FormCatalogValueText.Date(date),
        _ => null,
    };

    // La fila como la dibuja el editor: el valor, un número o una fecha ISO como sus campos (C6), y solo lo que se propone.
    private static Dictionary<string, object>? RowOf(OptionRow? row, FormCatalogValueType valueType)
    {
        if (row is null) return null;
        var view = new Dictionary<string, object>(StringComparer.Ordinal);
        if (row.Code is { } code) view[CodeField] = code;
        if (row.Text is { } text) view[TextField] = text;
        if (row.Detail is { } detail) view[DetailField] = detail;
        if (row.Active is { } active) view[ActiveField] = active;
        if (row.Value is { } value)
            view[ValueField] = valueType == FormCatalogValueType.Date
                ? value
                : decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return view;
    }

    // El código único por catálogo, sin distinguir mayúsculas (como guarda la ruta): entre lo propuesto, y contra lo guardado que
    // no se borra ni cambia de código en la misma propuesta.
    private async Task<Result> CheckCodesAsync(Guid catalogId, IReadOnlyList<(int Index, FormCatalogItemInput Input)> items,
        IReadOnlyCollection<Guid> deleted, CancellationToken cancellationToken)
    {
        var proposed = items.Select(item => (item.Index, item.Input.ItemId, Code: FormCatalogItem.NormalizeCode(item.Input.Code!))).ToArray();
        var first = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var option in proposed)
            if (!first.TryAdd(option.Code, option.Index)) return Invalid($"{OptionsField}[{option.Index}].{CodeField}", FormCatalogErrors.DuplicateCodeCode);

        var changing = proposed.Where(option => option.ItemId is not null).Select(option => option.ItemId!.Value).ToHashSet();
        var taken = await catalogs.ListItemsByCodesAsync(catalogId, first.Keys, cancellationToken);
        foreach (var option in proposed)
            if (taken.Any(item => item.CodeNormalized == option.Code && item.Id != option.ItemId && !deleted.Contains(item.Id) && !changing.Contains(item.Id)))
                return Invalid($"{OptionsField}[{option.Index}].{CodeField}", FormCatalogErrors.DuplicateCodeCode);
        return Result.Success();
    }

    // Lo busca el lector con su búsqueda, entre los catálogos a mano; igual gana y, si no hay, uno solo parecido.
    private async Task<Result<FormCatalogSummary>> MatchTargetAsync(string? text, CancellationToken cancellationToken)
    {
        if (!AssistantActionInputs.IsWritten(text)) return FormCatalogErrors.NotFound;
        var found = (await catalogs.ListAsync(new ListFormCatalogsRequest
        {
            Search = AssistantActionInputs.SearchOf(text),
            PageSize = AssistantActionLimits.Candidates,
            Source = FormCatalogSourceFilter.Manual,
        }, cancellationToken)).Items;
        var exact = found.Where(catalog => AssistantActionInputs.Same(catalog.Name, text)).ToArray();
        var candidates = exact.Length > 0 ? exact : found.Where(catalog => AssistantActionInputs.Contains(catalog.Name, text)).ToArray();
        return candidates.Length switch
        {
            0 => FormCatalogErrors.NotFound,
            // El editor no deja cambiar las opciones de uno dado de baja.
            1 when !candidates[0].IsActive => FormCatalogErrors.Inactive,
            1 => candidates[0],
            // Con varios catálogos parecidos no se elige uno: el modelo pregunta cuál.
            _ => FormCatalogErrors.Ambiguous,
        };
    }

    // El catálogo de una propuesta guardada, por su id: a mano y activo, como lo deja cambiar el editor.
    private async Task<FormCatalogSummary?> FindTargetAsync(Guid? catalogId, CancellationToken cancellationToken) =>
        catalogId is { } id && await catalogs.FindAsync(id, cancellationToken) is { Source: FormCatalogSource.Manual, IsActive: true } catalog
            ? catalog : null;

    // Las opciones guardadas que nombra la propuesta que siguen en el catálogo, leídas por id.
    private async Task<Dictionary<Guid, FormCatalogItemSnapshot>> SavedAsync(Guid catalogId, IReadOnlyList<OptionOp> ops,
        CancellationToken cancellationToken)
    {
        Guid[] ids = [.. ops.Where(op => op.Id is not null).Select(op => op.Id!.Value).Distinct()];
        return (await catalogs.ListItemsByIdsAsync(catalogId, ids, cancellationToken)).ToDictionary(item => item.Id);
    }

    private static AssistantActionField[] FieldsOf(FormCatalogValueType valueType) =>
    [
        new()
        {
            Name = OptionsField,
            Type = AssistantActionFieldType.Items,
            MaxItems = FormCatalogLimits.MaxDeleteItems,
            ItemKey = CodeField,
            ItemFields =
            [
                new() { Name = CodeField, Type = AssistantActionFieldType.Text, Required = true, MaxLength = FormsTextLimits.OptionCode },
                new() { Name = TextField, Type = AssistantActionFieldType.Text, Required = true, MaxLength = FormsTextLimits.OptionText },
                new() { Name = DetailField, Type = AssistantActionFieldType.LongText, MaxLength = FormsTextLimits.OptionDetail },
                new() { Name = ActiveField, Type = AssistantActionFieldType.Boolean },
                .. ValueInputs(valueType),
            ],
        },
    ];

    // El valor, solo en uno tipado y con el tipo del catálogo.
    private static AssistantActionField[] ValueInputs(FormCatalogValueType valueType) => valueType switch
    {
        FormCatalogValueType.WholeNumber => [new() { Name = ValueField, Type = AssistantActionFieldType.WholeNumber, Required = true }],
        FormCatalogValueType.DecimalNumber => [new() { Name = ValueField, Type = AssistantActionFieldType.DecimalNumber, Required = true }],
        FormCatalogValueType.Date => [new() { Name = ValueField, Type = AssistantActionFieldType.Date, Required = true }],
        _ => [],
    };

    // Una opción se nombra por su código o por su texto, como se ve en el editor.
    private static IEnumerable<string> OptionNames(FormCatalogItemRow item) => [item.Code, item.Text];

    private static OptionOp[] OpsOf(AssistantActionResolution resolution) =>
        AssistantActionInputs.ResolvedOf(resolution, OptionsField) is { ValueKind: JsonValueKind.Array } array
            ? array.Deserialize<OptionOp[]>(AssistantActionInputs.Json) ?? []
            : [];

    // El pedido nombra los campos del HTTP; la propuesta, los de la pantalla.
    private static string FieldOfProperty(string property) => property switch
    {
        nameof(UpdateFormCatalogRequest.Items) or nameof(UpdateFormCatalogRequest.DeleteItems) => OptionsField,
        _ => TargetPath,
    };

    private static string FieldOfCode(string code) => code switch
    {
        FormCatalogErrors.ItemTextTooLongCode => TextField,
        FormCatalogErrors.ItemDetailTooLongCode => DetailField,
        _ => ValueField,
    };

    private static ValidationError Invalid(string path, string code) => AssistantActionInputs.Invalid(path, code);

    /// <summary>Una operación resuelta sobre las opciones: sumar, cambiar o quitar, la opción guardada, lo que cambia y su duda.</summary>
    private sealed record OptionOp(
        [property: JsonPropertyName("op")] string Op,
        [property: JsonPropertyName("id")] Guid? Id,
        [property: JsonPropertyName("row")] OptionRow? Row,
        [property: JsonPropertyName("doubt")] string? Doubt);

    /// <summary>Lo que se propone de una opción, ya leído: el valor como texto canónico; null es lo que no se propone.</summary>
    private sealed record OptionRow(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("detail")] string? Detail,
        [property: JsonPropertyName("active")] bool? Active,
        [property: JsonPropertyName("value")] string? Value);

    /// <summary>Una fila de la propuesta como la dibuja el editor.</summary>
    private sealed record RowView(
        [property: JsonPropertyName("op")] string Op,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("row")] Dictionary<string, object>? Row);
}
