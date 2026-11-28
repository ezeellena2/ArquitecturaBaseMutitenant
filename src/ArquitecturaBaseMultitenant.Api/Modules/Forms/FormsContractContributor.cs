using System.Globalization;
using System.Resources;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using Microsoft.OpenApi;
using FormsPermissions = ArquitecturaBaseMultitenant.Domain.Authorization.Permissions.Forms;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms;

/// <summary>
/// Documenta las rutas de los catálogos (E12, y los campos de la E13): acceso, permiso, módulo, errores con sus textos y los topes
/// de las listas.
/// </summary>
internal sealed class FormsContractContributor : IHttpContractContributor
{
    private const string Prefix = "/api/organization/forms/catalogs";
    private static readonly ResourceManager Resources = new("ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources.FormsTexts", typeof(FormCatalogRow).Assembly);

    private static readonly (string Schema, string Property, int MaxItems)[] Bounds =
    [
        ("FormCatalogResponse", "roles", FormCatalogLimits.MaxFilterReferences), ("FormCatalogResponse", "companies", FormCatalogLimits.MaxFilterReferences),
        ("FormCatalogFilters", "conditions", FormCatalogLimits.MaxConditions), ("FormCatalogCondition", "values", FormCatalogLimits.MaxConditionValues),
        ("FormCatalogFiltersHttpInput", "conditions", FormCatalogLimits.MaxConditions), ("FormCatalogConditionHttpInput", "values", FormCatalogLimits.MaxConditionValues),
        ("FormCatalogSourceResponse", "columns", FormCatalogLimits.MaxSourceColumns), ("FormCatalogSourceColumn", "operators", FormCatalogLimits.MaxColumnOperators),
        ("FormCatalogSampleMemberRow", "companies", FormCatalogLimits.MaxSampleMemberNames), ("FormCatalogSampleMemberRow", "roles", FormCatalogLimits.MaxSampleMemberNames),
        ("FormCatalogPreviewResponse", "options", FormCatalogLimits.PreviewOptions),
        ("CreateFormCatalogHttpRequest", "items", FormCatalogLimits.MaxItems), ("UpdateFormCatalogHttpRequest", "items", FormCatalogLimits.MaxItems),
        ("UpdateFormCatalogHttpRequest", "moves", FormCatalogLimits.MaxMoves), ("UpdateFormCatalogHttpRequest", "deleteItems", FormCatalogLimits.MaxDeleteItems),
        ("FormCatalogCreatedResponse", "itemIdentityMap", FormCatalogLimits.MaxItems), ("FormCatalogUpdatedResponse", "itemIdentityMap", FormCatalogLimits.MaxItems),
        ("FormCatalogResponse", "fields", FormCatalogLimits.MaxStoredFields),
        ("FormCatalogPresentationDetail", "title", FormCatalogLimits.MaxPresentationPlaces), ("FormCatalogPresentationDetail", "description", FormCatalogLimits.MaxPresentationPlaces),
        ("FormCatalogPresentationHttpInput", "title", FormCatalogLimits.MaxPresentationPlaces), ("FormCatalogPresentationHttpInput", "description", FormCatalogLimits.MaxPresentationPlaces),
        ("FormCatalogPresentationIssues", "examples", FormCatalogLimits.PresentationExamples),
        ("CreateFormCatalogHttpRequest", "fields", FormCatalogLimits.MaxFields), ("UpdateFormCatalogHttpRequest", "fields", FormCatalogLimits.MaxFields),
        ("UpdateFormCatalogHttpRequest", "confirmedFieldRemovals", FormCatalogLimits.MaxFields),
        ("FormCatalogUpdatedResponse", "fieldIdentityMap", FormCatalogLimits.MaxFields),
    ];

    // Los topes de una opción, que el validador anidado no publica: la pantalla los lee de validationConstraints (269).
    private static readonly (string Schema, string Property, int MaxLength)[] Lengths =
    [
        ("FormCatalogItemHttpInput", "code", FormsTextLimits.OptionCode), ("FormCatalogItemHttpInput", "text", FormsTextLimits.OptionText),
        ("FormCatalogItemHttpInput", "detail", FormsTextLimits.OptionDetail),
        ("FormCatalogFieldHttpInput", "name", FormsTextLimits.FieldName), ("FormCatalogFieldHttpInput", "key", FormsTextLimits.FieldKey),
    ];

    /// <summary>La clave de un campo: nace del nombre, en minúsculas y sin acentos (E13).</summary>
    private const string FieldKeyPattern = "^[a-z][a-z0-9_]{0,39}$";

    public void Describe(OpenApiDocument document)
    {
        if (document.Components?.Schemas is { } schemas)
        {
            foreach (var (schema, property, maxItems) in Bounds)
                if (Property(schemas, schema, property) is { } value) value.MaxItems = maxItems;
            foreach (var (schema, property, maxLength) in Lengths)
                if (Property(schemas, schema, property) is { } value) value.MaxLength = maxLength;
            DescribeFields(schemas);
            // Los datos de DuplicateCode (n, la opción que ya tiene el código) y de ItemInUse (uses, quién la usa) van en el problema.
            if (schemas.TryGetValue("ProblemDetails", out var problemSchema) && problemSchema is OpenApiSchema problem)
            {
                problem.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
                problem.Properties["n"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32" };
                problem.Properties["uses"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                };
                // Un cambio de tipo o de regla que no entra (E13): cuántas opciones y las primeras, en orden de posición.
                problem.Properties["invalidCount"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32" };
                problem.Properties["invalidRows"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    MaxItems = FormCatalogLimits.MaxInvalidRows,
                    Items = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Required = new HashSet<string>(StringComparer.Ordinal) { "position", "itemId", "text", "reason" },
                        Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                        {
                            ["position"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                            ["itemId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                            ["text"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["reason"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Enum = [JsonValue.Create(FormCatalogInvalidRow.Required), JsonValue.Create(FormCatalogInvalidRow.Range),
                                    JsonValue.Create(FormCatalogInvalidRow.Length), JsonValue.Create(FormCatalogInvalidRow.Invalid)],
                            },
                        },
                    },
                };
            }
        }

        foreach (var (path, item) in document.Paths.Where(pair => pair.Key.StartsWith(Prefix, StringComparison.Ordinal)))
            foreach (var (method, operation) in item.Operations ?? [])
            {
                HttpContractDocumentation.Describe(document, path, method,
                    "Catálogos de Formularios (E12). La organización sale del token; un catálogo ajeno o con forms apagada es 404. "
                    + Description(path, method),
                    Errors(path, method), idempotent: IsIdempotent(path, method), authenticated: true);
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions["x-access"] = new JsonNodeExtension(JsonValue.Create("business")!);
                operation.Extensions["x-permissions"] = new JsonNodeExtension(new JsonArray(JsonValue.Create(Permission(path, method))));
                operation.Extensions["x-module-gate"] = new JsonNodeExtension(JsonValue.Create("feature:forms")!);
                if (IsIdempotent(path, method)) operation.Extensions["x-idempotency-mode"] = new JsonNodeExtension(JsonValue.Create("BufferedJson")!);
                // El filtro ids de las opciones (E13): hasta 100, repetido.
                foreach (var parameter in operation.Parameters ?? [])
                    if (parameter.Name == "Ids" && parameter.Schema is OpenApiSchema ids) ids.MaxItems = FormCatalogLimits.MaxItemIds;
                foreach (var response in operation.Responses?.Values.AsEnumerable() ?? [])
                    if (response.Content?.ContainsKey("application/json") == true)
                    { response.Content.Remove("text/plain"); response.Content.Remove("text/json"); }
            }
    }

    /// <summary>Los POST que crean o aplican llevan Idempotency-Key; las vistas previas no escriben y no la llevan.</summary>
    private static bool IsIdempotent(string path, HttpMethod method) =>
        method == HttpMethod.Post && (path == Prefix || path.EndsWith("/imports", StringComparison.Ordinal));

    private static string Permission(string path, HttpMethod method) =>
        method == HttpMethod.Put || method == HttpMethod.Post && !path.EndsWith("/catalogs/previews", StringComparison.Ordinal)
            || path.Contains("/sources/", StringComparison.Ordinal)
            ? FormsPermissions.CatalogsManage : FormsPermissions.CatalogsRead;

    private static List<HttpContractError> Errors(string path, HttpMethod method)
    {
        var errors = new List<HttpContractError>
        {
            HttpContractDocumentation.CoreError(401, "Http.Unauthorized"),
            HttpContractDocumentation.CoreError(403, "Http.Forbidden"),
            HttpContractDocumentation.CoreError(403, "Legal.AcceptanceRequired"),
            HttpContractDocumentation.CoreError(404, "Http.NotFound"),
            HttpContractDocumentation.CoreError(500, "General.Unexpected"),
        };
        void Add(int status, string code, string? field = null) => errors.Add(new HttpContractError(status, code, field,
            Resources.GetString(code, CultureInfo.GetCultureInfo("es")) ?? code, Resources.GetString(code, CultureInfo.GetCultureInfo("en")) ?? code));
        var withId = path.Contains("{id}", StringComparison.Ordinal);
        var create = path == Prefix && method == HttpMethod.Post;
        var update = withId && method == HttpMethod.Put;
        var preview = path.EndsWith("/imports/previews", StringComparison.Ordinal);
        var apply = path.EndsWith("/imports", StringComparison.Ordinal);
        var export = path.EndsWith("/export", StringComparison.Ordinal);
        var filtersPreview = path == Prefix + "/previews";
        var template = path.EndsWith("/template", StringComparison.Ordinal);
        if (!(withId && method == HttpMethod.Get && !path.EndsWith("/items", StringComparison.Ordinal)) && !path.EndsWith("/filter-counts", StringComparison.Ordinal)
            && !path.EndsWith("/sources/members", StringComparison.Ordinal))
            // La plantilla solo recibe el tipo: uno que no existe no llega al validador y lo rechaza el binding.
            errors.Add(path.EndsWith("/template", StringComparison.Ordinal)
                ? HttpContractDocumentation.CoreError(400, ApiErrorCodes.InvalidRequest)
                : HttpContractDocumentation.CoreError(400, "Validation.Failed"));
        if (IsIdempotent(path, method))
            errors.AddRange([HttpContractDocumentation.CoreError(400, "Request.IdempotencyKeyRequired"),
                HttpContractDocumentation.CoreError(422, "Request.IdempotencyKeyReused"), HttpContractDocumentation.CoreError(409, "Request.InProgress")]);
        if (update || preview || apply) errors.Add(HttpContractDocumentation.CoreError(409, "General.ConcurrencyConflict", "version"));
        if (withId || template) Add(404, FormCatalogErrors.NotFoundCode);
        if (create || update || filtersPreview)
        {
            Add(400, FormCatalogErrors.SampleRequiredCode, "sampleMemberId");
            Add(400, FormCatalogErrors.FiltersInvalidCode, "conditions.{i}");
            Add(400, FormCatalogErrors.SampleMemberNotFoundCode, "sampleMemberId");
            Add(400, FormCatalogErrors.SampleContactNotFoundCode, "sampleContactId");
        }
        if (create || update)
        {
            Add(400, FormCatalogErrors.DuplicateCodeCode, "items.{i}.code");
            Add(400, FormCatalogErrors.ValueRequiredCode, "items.{i}.value");
            Add(400, FormCatalogErrors.ValueInvalidCode, "items.{i}.value");
            Add(400, FormCatalogErrors.ItemTextTooLongCode, "items.{i}.text");
            Add(400, FormCatalogErrors.ItemDetailTooLongCode, "items.{i}.detail");
            Add(409, FormCatalogErrors.WrongSourceCode);
            Add(409, FormCatalogErrors.TooManyItemsCode);
            // Los campos de las opciones y la presentación (E13).
            Add(400, FormCatalogErrors.TooManyFieldsCode, "fields");
            Add(400, FormCatalogErrors.FieldKeyDuplicateCode, "fields.{i}.key");
            Add(400, FormCatalogErrors.FieldInvalidCode, "fields.{i}");
            Add(400, FormCatalogErrors.PresentationInvalidCode, "presentation");
        }
        if (update)
        {
            Add(404, FormCatalogErrors.ItemNotFoundCode, "deleteItems.{i}");
            Add(409, FormCatalogErrors.ItemInUseCode, "deleteItems.{i}");
            Add(409, FormCatalogErrors.MoveOutOfRangeCode);
            Add(409, FormCatalogErrors.InactiveCode);
            Add(400, FormCatalogErrors.FieldRemovalNotConfirmedCode, "fields.{i}");
            Add(400, FormCatalogErrors.FieldTypeNotConvertibleCode, "fields.{i}.type");
            Add(400, FormCatalogErrors.FieldValuesIncompatibleCode, "fields.{i}");
            Add(400, FormCatalogErrors.FieldValuesMissingCode, "fields.{i}.required");
            Add(400, FormCatalogErrors.FieldValueInvalidCode, "items.{i}.fields.{fieldId}");
            Add(400, FormCatalogErrors.FieldValueRequiredCode, "items.{i}.fields.{fieldId}");
            Add(400, FormCatalogErrors.FieldValueDuplicateCode, "items.{i}.fields.{fieldId}");
            Add(409, FormCatalogErrors.SourceInUseCode);
        }
        if (preview || apply)
        {
            Add(400, FormCatalogImportErrors.FileInvalidCode, "file");
            Add(409, FormCatalogImportErrors.BusyCode);
            Add(409, FormCatalogErrors.InactiveCode);
            Add(409, FormCatalogErrors.WrongSourceCode);
            Add(429, FormCatalogImportErrors.TooManyAtOnceCode);
        }
        if (apply)
        {
            Add(400, FormCatalogImportErrors.ConfirmationRequiredCode);
            Add(409, FormCatalogImportErrors.FileChangedCode);
            Add(409, FormCatalogErrors.TooManyItemsCode);
        }
        if (export)
        {
            Add(409, FormCatalogImportErrors.BusyCode);
            Add(409, FormCatalogErrors.WrongSourceCode);
            Add(429, FormCatalogImportErrors.TooManyAtOnceCode);
        }
        return errors;
    }

    private static string Description(string path, HttpMethod method)
    {
        if (path == Prefix) return method == HttpMethod.Get
            ? "Listado paginado (10 por defecto; 20, 50 o 100) con search, source (all, manual, members o contacts), status (all, active o inactive) y sort por name, source, valueType, optionsCount o status, con - delante el otro sentido (264); optionsCount nulo va al final en los dos sentidos y source, valueType y status ordenan por su orden."
            : "Alta con Location: sin tipo, Text; Members con condiciones exige sampleMemberId. Uno a mano puede traer fields, que nacen sin valores, y presentation (E13). Devuelve el detalle y el mapa de ids de las opciones nuevas.";
        if (path.EndsWith("/filter-counts", StringComparison.Ordinal)) return "Solo {all, active}: lo que dice el resumen del listado (262).";
        if (path.EndsWith("/items", StringComparison.Ordinal)) return "Opciones por posición, paginadas, con search y status (active, inactive o all), y ids repetido (hasta 100 y sin vacíos) para nombrar las opciones de una fuente. Cada opción trae fields (el valor canónico de cada campo por su id: número como texto, fecha yyyy-MM-dd, sí/no booleano y una fuente con el id de su opción) y presentationIssue (E13).";
        if (path.EndsWith("/imports/previews", StringComparison.Ordinal)) return "Revisa el archivo o lo pegado (mode append al pegar, replace con un archivo) sin escribir: versión leída, SHA-256 y las cuatro listas paginadas con el page y el pageSize del pedido. Una columna por campo activo, por su nombre o su clave, con sus errores por fila y columna (Forms.Catalog.FieldValueInvalid, FieldValueRequired y FieldValueDuplicate, E13). De a una por persona (Busy) y con lugar en la organización y la instalación (429 TooManyAtOnce, con Retry-After).";
        if (path.EndsWith("/imports", StringComparison.Ordinal)) return "Aplica lo revisado en una UoW: el mismo archivo (409 FileChanged si el SHA-256 difiere), la versión de la revisión, skipErrorRows y confirmedDeactivations con la cantidad exacta. Más del 25 % de bajas o del 10 % de filas con error sin confirmar es 400 ConfirmationRequired.";
        if (path.EndsWith("/export", StringComparison.Ordinal)) return "XLSX con todas las opciones, activas y dadas de baja, con las columnas de la plantilla (C10) y una por campo activo, como en pantalla (119). De a una por persona.";
        if (path.EndsWith("/template", StringComparison.Ordinal)) return "XLSX de la plantilla para el valueType pedido, sin barrera (C11); con catalogId, también las columnas de los campos activos de ese catálogo (E13), y uno ajeno o que no existe es 404.";
        if (path.EndsWith("/sources/members", StringComparison.Ordinal)) return "La fuente «Miembros»: su versión y el catálogo cerrado de columnas y operadores (§4).";
        if (path.Contains("/sources/", StringComparison.Ordinal)) return "Lista paginada con search y orden por name, para editar los filtros.";
        if (path.EndsWith("/sample-members", StringComparison.Ordinal)) return "Personas de ejemplo de la organización, sin correo, con los nombres de sus empresas y sus roles.";
        if (path.EndsWith("/sample-contacts", StringComparison.Ordinal)) return "Clientes de ejemplo de Clientes, sin teléfono (C12).";
        if (path.EndsWith("/previews", StringComparison.Ordinal)) return "Vista previa de unos filtros sin guardar con la persona o el cliente de ejemplo como destinatario; no escribe. Hasta 200 opciones, total exacto y exceedsLimit.";
        return method == HttpMethod.Get
            ? "Detalle con los nombres de los roles y las empresas del filtro, la persona o el cliente de ejemplo, canManage, itemCounts y quién lo guardó por última vez. Desde la E13, fields (los campos en su orden, también los quitados, con valuesCount), presentation y presentationIssues ({count, examples}, hasta tres)."
            : "Guarda todo junto con su versión (C3): items solo con lo nuevo o cambiado (con el delta de sus fields: ausente conserva y null limpia), moves, sort o deleteItems (hasta 500), que no se combinan entre sí. sort.column es Text, Code o el id de un campo activo guardado. fields es la lista entera de los campos activos si cambió, con id o localId; confirmedFieldRemovals, los campos con valores que se quitan; presentation, qué va en WhatsApp (E13). Devuelve la versión y los mapas de ids de las opciones y de los campos nuevos.";
    }

    /// <summary>
    /// Los campos de las opciones (E13): el valor de cada campo va por la identidad del campo, como texto (un número o una fecha
    /// <c>yyyy-MM-dd</c> o la identidad de una opción fuente) o como booleano; en el delta de una opción, <c>null</c> lo limpia.
    /// </summary>
    private static void DescribeFields(IDictionary<string, IOpenApiSchema> schemas)
    {
        if (Property(schemas, "FormCatalogItemRow", "fields") is { } saved)
            saved.AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Boolean };
        if (Property(schemas, "FormCatalogItemHttpInput", "fields") is { } delta)
        {
            delta.AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Boolean | JsonSchemaType.Null };
            delta.MaxProperties = FormCatalogLimits.MaxStoredFields;
        }
        if (Property(schemas, "FormCatalogFieldHttpInput", "key") is { } key) key.Pattern = FieldKeyPattern;
        if (Property(schemas, "FormCatalogFieldHttpInput", "maxLength") is { } length)
        {
            length.Minimum = "1";
            length.Maximum = FormsTextLimits.FieldTextLength.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static OpenApiSchema? Property(IDictionary<string, IOpenApiSchema> schemas, string schema, string property) =>
        schemas.TryGetValue(schema, out var value) && value is OpenApiSchema found && found.Properties?.TryGetValue(property, out var member) == true
            ? member as OpenApiSchema : null;
}
