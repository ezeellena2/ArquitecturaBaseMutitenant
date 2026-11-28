using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// La acción de las opciones de un catálogo (§17 de la E12) resuelve las opciones que nombra el modelo con una sola lectura de sus
/// parecidos, aunque nombre las 500 que admite: una lectura por opción eran 1.500 consultas en un pedido. Desde la E13, cada campo
/// activo del catálogo destino es una columna de la fila, con su tipo y sus reglas, y su valor se lee como lo pide el <c>PUT</c>.
/// </summary>
public sealed class FormCatalogActionSourceTests
{
    private const string Catalog = "Colores";
    private const int Named = FormCatalogLimits.MaxDeleteItems;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Naming_the_most_options_reads_their_candidates_once()
    {
        var reader = new CatalogReader(Enumerable.Range(1, Named + 20)
            .Select(index => new FormCatalogItemCandidateRow(string.Empty, Guid.NewGuid(), $"C{index:D3}", $"Opción {index:D3}")).ToArray());
        var source = Source(reader);
        var options = Enumerable.Range(1, Named).Select(index => new { op = "change", key = $"c{index:D3}", row = new { active = false } });
        var draft = new AssistantActionDraft(FormCatalogActionSource.Actions[0], Catalog,
            [new(FormCatalogActionSource.OptionsField, JsonSerializer.SerializeToElement(options))]);

        var resolved = await source.ResolveAsync(draft, Ct);

        Assert.True(resolved.IsSuccess, resolved.IsFailure ? resolved.Error.Code : null);
        Assert.Equal(1, reader.CandidateReads);
        Assert.Equal(0, reader.ItemPages);
        var ids = resolved.Value.Values.Single().Resolved!.Value.EnumerateArray().Select(op => op.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(reader.Items.Take(Named).Select(item => item.Id), ids);
    }

    [Fact]
    public async Task A_missing_option_goes_back_to_the_model_and_the_others_use_the_same_read()
    {
        CatalogReader reader = new([new(string.Empty, Guid.NewGuid(), "AZU", "Azul"), new(string.Empty, Guid.NewGuid(), "AZM", "Azul marino")]);
        var source = Source(reader);
        object[] options = [new { op = "change", key = "azul", row = new { detail = "El del cielo" } }, new { op = "remove", key = "Verde" }];
        var draft = new AssistantActionDraft(FormCatalogActionSource.Actions[0], Catalog,
            [new(FormCatalogActionSource.OptionsField, JsonSerializer.SerializeToElement(options))]);

        var resolved = await source.ResolveAsync(draft, Ct);

        // «Verde» no está: la segunda fila vuelve al modelo, y la primera se resolvió con la misma lectura.
        Assert.True(resolved.IsFailure);
        var errors = Assert.IsType<ValidationError>(resolved.Error).Errors;
        Assert.Equal([FormCatalogErrors.ItemNotFoundCode], Assert.Single(errors, error => error.Key == $"{FormCatalogActionSource.OptionsField}[1]").Value);
        Assert.Single(errors);
        Assert.Equal(1, reader.CandidateReads);
    }

    [Fact]
    public async Task The_target_fields_add_a_column_per_active_field_with_its_type_and_its_rules_as_limits()
    {
        var reader = new CatalogReader([], Fields());
        var source = Source(reader);

        var fields = await source.ReadTargetFieldsAsync(FormCatalogActionSource.Actions[0], Catalog, Ct);

        Assert.True(fields.IsSuccess, fields.IsFailure ? fields.Error.Code : null);
        var columns = Assert.Single(fields.Value).ItemFields!;
        // El quitado no es columna, y «active» tampoco: es la columna fija del estado.
        Assert.Equal(["code", "text", "detail", "active", "cuit", "nota", "plazo", "peso", "alta", "iva", "zona"], columns.Select(column => column.Name));
        Assert.Single(columns, column => column.Name == "active");
        var byName = columns.Skip(4).ToDictionary(column => column.Name);
        Assert.Equal((AssistantActionFieldType.Text, (int?)13, false), (byName["cuit"].Type, byName["cuit"].MaxLength, byName["cuit"].Required));
        Assert.Equal((AssistantActionFieldType.Text, (int?)FormsTextLimits.FieldTextLength), (byName["nota"].Type, byName["nota"].MaxLength));
        Assert.Equal((AssistantActionFieldType.WholeNumber, (decimal?)0m, (decimal?)90m), (byName["plazo"].Type, byName["plazo"].Minimum, byName["plazo"].Maximum));
        Assert.Equal((AssistantActionFieldType.DecimalNumber, (decimal?)0.5m, (decimal?)null), (byName["peso"].Type, byName["peso"].Minimum, byName["peso"].Maximum));
        // Una fecha no lleva límites: el contrato de la acción solo tiene mínimo y máximo numéricos.
        Assert.Equal((AssistantActionFieldType.Date, (decimal?)null, (decimal?)null), (byName["alta"].Type, byName["alta"].Minimum, byName["alta"].Maximum));
        Assert.Equal(AssistantActionFieldType.Boolean, byName["iva"].Type);
        Assert.Equal(AssistantActionFieldType.Record, byName["zona"].Type);
        Assert.All(byName.Values, column => Assert.False(column.Required));
    }

    [Fact]
    public async Task A_row_reads_each_field_by_its_key_as_the_route_takes_it_and_names_the_source_option_by_its_text()
    {
        var reader = new CatalogReader([], Fields());
        var source = Source(reader);
        object[] options = [new { op = "add", row = new Dictionary<string, object?>
        {
            ["code"] = "NUE", ["text"] = "Nuevo", ["cuit"] = null, ["plazo"] = 30, ["peso"] = 2.5m, ["alta"] = "2026-12-01", ["iva"] = true,
            ["zona"] = "norte",
        } }];
        var draft = new AssistantActionDraft(FormCatalogActionSource.Actions[0], Catalog,
            [new(FormCatalogActionSource.OptionsField, JsonSerializer.SerializeToElement(options))]);

        var resolved = await source.ResolveAsync(draft, Ct);

        Assert.True(resolved.IsSuccess, resolved.IsFailure ? string.Join(", ", ((ValidationError)resolved.Error).Errors.Keys) : null);
        var fields = resolved.Value.Values.Single().Resolved!.Value[0].GetProperty("row").GetProperty("fields");
        Assert.Equal(JsonValueKind.Null, fields.GetProperty("cuit").ValueKind);
        Assert.Equal("30", fields.GetProperty("plazo").GetString());
        Assert.Equal("2.5", fields.GetProperty("peso").GetString());
        Assert.Equal("2026-12-01", fields.GetProperty("alta").GetString());
        Assert.True(fields.GetProperty("iva").GetBoolean());
        // La opción de la fuente, por su identidad: el modelo la nombró por su texto, en una sola lectura de la fuente.
        Assert.Equal(North.ToString("D"), fields.GetProperty("zona").GetString());
        Assert.Equal(1, reader.CandidateReads);
    }

    [Fact]
    public async Task A_value_outside_its_rules_or_a_source_option_that_is_not_there_goes_back_to_the_model_with_its_path()
    {
        var reader = new CatalogReader([], Fields());
        var source = Source(reader);
        object[] options =
        [
            new { op = "add", row = new Dictionary<string, object?>
            {
                ["code"] = "NUE", ["text"] = "Nuevo", ["plazo"] = 120, ["cuit"] = new string('9', 14), ["alta"] = "01/12/2026", ["viejo"] = "Algo",
            } },
            new { op = "add", row = new Dictionary<string, object?> { ["code"] = "OTR", ["text"] = "Otro", ["zona"] = "Oeste" } },
        ];
        var draft = new AssistantActionDraft(FormCatalogActionSource.Actions[0], Catalog,
            [new(FormCatalogActionSource.OptionsField, JsonSerializer.SerializeToElement(options))]);

        var resolved = await source.ResolveAsync(draft, Ct);

        var errors = Assert.IsType<ValidationError>(resolved.Error).Errors;
        foreach (var key in new[] { "plazo", "cuit", "alta" })
            Assert.Equal([FormCatalogErrors.FieldValueInvalidCode], errors[$"{FormCatalogActionSource.OptionsField}[0].{key}"]);
        // Un campo quitado no es una columna del editor.
        Assert.Equal([ValidationError.ErrorCode], errors[$"{FormCatalogActionSource.OptionsField}[0]"]);
        Assert.Equal([FormCatalogErrors.FieldValueInvalidCode], errors[$"{FormCatalogActionSource.OptionsField}[1].zona"]);
    }

    private static readonly Guid Zones = Guid.NewGuid();
    private static readonly Guid North = Guid.NewGuid();

    /// <summary>Un campo de cada tipo, uno quitado y uno con la clave de la columna fija del estado.</summary>
    private static FormCatalogField[] Fields() =>
    [
        new(Guid.NewGuid(), "cuit", "CUIT", FormCatalogFieldType.Text, null, false, true, 13, null, null, 0, true),
        new(Guid.NewGuid(), "nota", "Nota", FormCatalogFieldType.Text, null, false, false, null, null, null, 1, true),
        new(Guid.NewGuid(), "viejo", "Viejo", FormCatalogFieldType.Text, null, false, false, null, null, null, 2, false),
        new(Guid.NewGuid(), "plazo", "Plazo", FormCatalogFieldType.WholeNumber, null, false, false, null, "0", "90", 3, true),
        new(Guid.NewGuid(), "peso", "Peso", FormCatalogFieldType.DecimalNumber, null, false, false, null, "0.5", null, 4, true),
        new(Guid.NewGuid(), "alta", "Alta", FormCatalogFieldType.Date, null, false, false, null, "2026-01-01", "2026-12-31", 5, true),
        new(Guid.NewGuid(), "iva", "IVA", FormCatalogFieldType.YesNo, null, false, false, null, null, null, 6, true),
        new(Guid.NewGuid(), "zona", "Zona", FormCatalogFieldType.Catalog, Zones, false, false, null, null, null, 7, true),
        new(Guid.NewGuid(), "active", "Activo", FormCatalogFieldType.YesNo, null, false, false, null, null, null, 8, true),
    ];

    private static FormCatalogActionSource Source(CatalogReader reader)
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-28T15:00:00Z", CultureInfo.InvariantCulture));
        var formatter = new DisplayFormatter(catalog, catalog, catalog, catalog, catalog, new LibPhoneNumberDisplayFormatter(),
            new TimeZoneService(clock), clock);
        return new FormCatalogActionSource(reader, [], new FormCatalogFieldValuePolicy(formatter, reader));
    }

    private sealed class CatalogReader(IReadOnlyList<FormCatalogItemCandidateRow> items, IReadOnlyList<FormCatalogField>? fields = null)
        : IFormCatalogReader
    {
        // «Zonas», la fuente del campo «zona».
        private static readonly FormCatalogItemCandidateRow[] ZoneItems =
            [new(string.Empty, North, "N", "Norte"), new(string.Empty, Guid.NewGuid(), "S", "Sur")];

        private readonly FormCatalogStoredRow _catalog = new(Guid.NewGuid(), Catalog, null, FormCatalogSource.Manual, FormCatalogValueType.Text,
            null, IsActive: true, items.Count, items.Count, new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc), null, 7);

        internal IReadOnlyList<FormCatalogItemCandidateRow> Items => items;

        internal int CandidateReads { get; private set; }

        internal int ItemPages { get; private set; }

        public Task<PagedResult<FormCatalogStoredRow>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct) =>
            Task.FromResult(new PagedResult<FormCatalogStoredRow>([_catalog], 1, request.PageSize, 1));

        public Task<FormCatalogStoredRow?> FindAsync(Guid catalogId, CancellationToken ct) =>
            Task.FromResult<FormCatalogStoredRow?>(catalogId == _catalog.Id ? _catalog : null);

        public Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request, CancellationToken ct)
        {
            ItemPages++;
            FormCatalogItemRow[] found = [.. Matching(items, request.Search!).Take(request.PageSize)
                .Select(item => new FormCatalogItemRow(item.Id, item.Code, item.Text, null, null, 0, true, false, FormCatalogItemRow.NoFields, null))];
            return Task.FromResult(new PagedResult<FormCatalogItemRow>(found, 1, request.PageSize, found.Length));
        }

        public Task<IReadOnlyList<FormCatalogItemCandidateRow>> ListItemCandidatesAsync(Guid catalogId, IReadOnlyCollection<string> searches,
            int perSearch, CancellationToken ct)
        {
            CandidateReads++;
            var pool = catalogId == Zones ? ZoneItems : items;
            return Task.FromResult<IReadOnlyList<FormCatalogItemCandidateRow>>([.. searches.Distinct(StringComparer.Ordinal)
                .SelectMany(search => Matching(pool, search).Take(perSearch).Select(item => item with { Search = search }))]);
        }

        public Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<FormCatalogFieldsRow?> FindFieldsAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();

        public Task<FormCatalogPresentationRow?> FindPresentationAsync(Guid catalogId, CancellationToken ct) =>
            Task.FromResult<FormCatalogPresentationRow?>(catalogId == _catalog.Id ? new(FormCatalogField.ToJson(fields ?? []), "{}") : null);

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListActiveItemBatchAsync(Guid catalogId, int? afterPosition, int take,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogFieldSourceRow>> ListFieldSourcesAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
            CancellationToken ct) => throw new NotSupportedException();

        private static IEnumerable<FormCatalogItemCandidateRow> Matching(IEnumerable<FormCatalogItemCandidateRow> pool, string search) => pool.Where(item =>
            CultureInfo.InvariantCulture.CompareInfo.IndexOf(item.Code, search, CompareOptions.IgnoreCase) >= 0
            || CultureInfo.InvariantCulture.CompareInfo.IndexOf(item.Text, search, CompareOptions.IgnoreCase) >= 0);
    }
}
