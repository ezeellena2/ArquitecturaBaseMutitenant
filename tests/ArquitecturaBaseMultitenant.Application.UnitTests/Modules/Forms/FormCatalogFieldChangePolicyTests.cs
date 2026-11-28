using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
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
/// Cambiar el tipo o las reglas de un campo guardado (E13, B5): las conversiones (a texto, el texto que se ve: el de la opción fuente,
/// aunque esté dada de baja, «Sí» o «No», y el número y la fecha en la cultura; desde texto, leído con la cultura del pedido, sin lo
/// que se lee de dos formas); lo que no entra, con las primeras 50 opciones en orden de posición e <c>invalidCount</c>, sin escribir
/// ninguna; y obligatorio y único sobre las opciones activas, con lo que manda el mismo guardado.
/// </summary>
public sealed class FormCatalogFieldChangePolicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Zones = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid North = Guid.NewGuid();
    private static readonly Guid South = Guid.NewGuid();
    private static readonly FormCatalogItemChanges NoItems = new([], [], null, []);

    [Theory]
    [InlineData("es-AR", "Sí", "No", "1.500", "1.234,5")]
    [InlineData("en-US", "Yes", "No", "1,500", "1,234.5")]
    public async Task Converting_to_text_leaves_the_text_that_is_seen_also_of_an_inactive_source_option(string culture, string yes,
        string no, string whole, string number)
    {
        Assert.Equal([yes, no], await ToTextAsync(culture, FormCatalogFieldType.YesNo, Json(true), Json(false)));
        Assert.Equal(["Norte", "Sur"], await ToTextAsync(culture, FormCatalogFieldType.Catalog, Json(North.ToString()), Json(South.ToString())));
        Assert.Equal([whole, number], await ToTextAsync(culture, FormCatalogFieldType.DecimalNumber, Json("1500"), Json("1234.5")));
        var date = await Formatter().CreateAsync(culture, TimeZoneInfo.Utc.Id, Ct);
        Assert.Equal([Formatter().FormatDate(new DateOnly(2026, 9, 27), date)], await ToTextAsync(culture, FormCatalogFieldType.Date, Json("2026-09-27")));
    }

    [Fact]
    public async Task Converting_from_text_reads_with_the_request_culture_and_what_reads_two_ways_does_not_convert_nor_write()
    {
        var setup = Catalog(FormCatalogFieldType.Text, Json("1500"), Json("1.500"), Json("doce"), Json(""), null);

        var result = await setup.ChangeAsync("es-AR", Field(setup, FormCatalogFieldType.WholeNumber));

        var (key, message) = Single(result.Error);
        Assert.Equal(FormCatalogErrors.FieldTypeNotConvertibleCode, result.Error.Code);
        Assert.Equal("fields.0.type", key);
        Assert.Equal("2 opciones tienen un valor que no es un número entero.", message);
        Assert.Equal(2, result.Error.Metadata!["invalidCount"]);
        var rows = Rows(result.Error);
        Assert.Equal([(1, setup.Items[1].Id, "Opción 1", FormCatalogInvalidRow.Invalid), (2, setup.Items[2].Id, "Opción 2", FormCatalogInvalidRow.Invalid)],
            rows.Select(row => (row.Position, row.ItemId, row.Text, row.Reason)));
        Assert.Equal(0, setup.Writes);

        var fixedSetup = Catalog(FormCatalogFieldType.Text, Json("1500"), Json("27/09/2026"), Json(""));
        var dates = await fixedSetup.ChangeAsync("es-AR", Field(fixedSetup, FormCatalogFieldType.Date),
            changes: new([fixedSetup.Edit(0, Json("2026-01-02"))], [], null, []));
        Assert.True(dates.IsSuccess, dates.IsFailure ? dates.Error.Code : null);
        Assert.Equal("2026-09-27", fixedSetup.Value(1)?.GetString());
        Assert.Null(fixedSetup.Value(2));
        // La que trae su valor en el pedido no se convierte: su valor llega en el tipo nuevo.
        Assert.Equal("1500", fixedSetup.Value(0)?.GetString());
    }

    [Fact]
    public async Task Between_numbers_and_sources_only_what_is_still_of_the_type_converts()
    {
        var decimals = Catalog(FormCatalogFieldType.DecimalNumber, Json("2"), Json("1.5"));
        var whole = await decimals.ChangeAsync("es-AR", Field(decimals, FormCatalogFieldType.WholeNumber));
        Assert.Equal([1], Rows(whole.Error).Select(row => row.Position));

        var zones = Catalog(FormCatalogFieldType.Catalog, Json(North.ToString()));
        var otherSource = await zones.ChangeAsync("es-AR", Field(zones, FormCatalogFieldType.Catalog) with { SourceCatalogId = Other });
        Assert.Equal(FormCatalogErrors.FieldTypeNotConvertibleCode, otherSource.Error.Code);
        Assert.Equal("1 opción tiene un valor que no es una opción de su catálogo.", Single(otherSource.Error).Message);

        var texts = Catalog(FormCatalogFieldType.Text, Json(North.ToString()));
        Assert.True((await texts.ChangeAsync("es-AR", Field(texts, FormCatalogFieldType.Catalog) with { SourceCatalogId = Zones })).IsSuccess);
        Assert.Equal(North.ToString("D"), texts.Value(0)?.GetString());
    }

    [Fact]
    public async Task Invalid_rows_are_the_first_50_in_position_order_across_batches_with_the_total()
    {
        var count = FormCatalogFieldChangePolicy.ItemsPerBatch + 20;
        var setup = Catalog(FormCatalogFieldType.Text, [.. Enumerable.Range(0, count).Select(index => (JsonElement?)Json(index < 10 ? "7" : "x" + index))]);

        var result = await setup.ChangeAsync("en-US", Field(setup, FormCatalogFieldType.WholeNumber));

        Assert.Equal(count - 10, result.Error.Metadata!["invalidCount"]);
        Assert.Equal(Enumerable.Range(10, FormCatalogLimits.MaxInvalidRows), Rows(result.Error).Select(row => row.Position));
        Assert.Equal("510 options have a value that isn't a whole number.", Single(result.Error).Message);
        Assert.Equal(2, setup.BatchReads);
    }

    [Fact]
    public async Task A_new_limit_without_changing_the_type_is_values_incompatible_with_its_reason()
    {
        var texts = Catalog(FormCatalogFieldType.Text, Json("abcd"), Json("ab"));
        var length = await texts.ChangeAsync("es-AR", Field(texts, FormCatalogFieldType.Text) with { MaxLength = 3 });
        Assert.Equal(FormCatalogErrors.FieldValuesIncompatibleCode, length.Error.Code);
        Assert.Equal(("fields.0", "1 opción no cumple la regla nueva."), Single(length.Error));
        Assert.Equal([(0, FormCatalogInvalidRow.Length)], Rows(length.Error).Select(row => (row.Position, row.Reason)));

        var numbers = Catalog(FormCatalogFieldType.WholeNumber, Json("5"), Json("50"));
        var range = await numbers.ChangeAsync("es-AR", Field(numbers, FormCatalogFieldType.WholeNumber) with { Min = "10" });
        Assert.Equal([(0, FormCatalogInvalidRow.Range)], Rows(range.Error).Select(row => (row.Position, row.Reason)));
    }

    [Fact]
    public async Task Required_counts_the_active_options_without_a_value_as_they_remain_after_the_same_save()
    {
        var setup = Catalog(FormCatalogFieldType.Text, Json("A"), null, Json(""), null, null);
        setup.Items[3].Deactivate();

        var missing = await setup.ChangeAsync("es-AR", Field(setup, FormCatalogFieldType.Text) with { Required = true });
        Assert.Equal(FormCatalogErrors.FieldValuesMissingCode, missing.Error.Code);
        Assert.Equal(("fields.0.required", "3 opciones activas no tienen valor."), Single(missing.Error));

        // La que trae su valor, la que se da de baja y la que se borra en el mismo guardado ya no cuentan.
        var one = await setup.ChangeAsync("es-AR", Field(setup, FormCatalogFieldType.Text) with { Required = true },
            changes: new([setup.Edit(1, Json("B")), setup.Deactivate(2)], [], null, []));
        Assert.Equal(("fields.0.required", "1 opción activa no tiene valor."), Single(one.Error));
        var none = await setup.ChangeAsync("es-AR", Field(setup, FormCatalogFieldType.Text) with { Required = true },
            changes: new([setup.Edit(1, Json("B")), setup.Deactivate(2)], [], null, [setup.Items[4].Id]));
        Assert.True(none.IsSuccess, none.IsFailure ? none.Error.Code : null);
    }

    [Fact]
    public async Task Unique_compares_the_active_options_and_a_value_of_the_same_save_that_repeats_a_saved_one_is_its_duplicate()
    {
        var setup = Catalog(FormCatalogFieldType.Text, Json("A"), Json(" a "), Json("B"), Json("A"), Json("C"));
        setup.Items[3].Deactivate();

        var repeated = await setup.ChangeAsync("es-AR", Field(setup, FormCatalogFieldType.Text) with { Unique = true });
        Assert.Equal(FormCatalogErrors.FieldValuesIncompatibleCode, repeated.Error.Code);
        Assert.Equal(1, repeated.Error.Metadata!["invalidCount"]);
        Assert.Equal([(1, setup.Items[1].Id, FormCatalogInvalidRow.Invalid)], Rows(repeated.Error).Select(row => (row.Position, row.ItemId, row.Reason)));

        var duplicate = await setup.ChangeAsync("es-AR", Field(setup, FormCatalogFieldType.Text) with { Unique = true },
            changes: new([setup.Edit(1, Json("Z")), setup.Edit(4, Json(" b "))], [], null, []));
        Assert.Equal(FormCatalogErrors.FieldValueDuplicateCode, duplicate.Error.Code);
        Assert.Equal(($"items.1.fields.{setup.FieldId}", "Ya lo tiene la opción 3."), Single(duplicate.Error));
    }

    [Fact]
    public async Task A_change_that_every_option_admits_writes_only_the_options_whose_value_changes()
    {
        var setup = Catalog(FormCatalogFieldType.WholeNumber, Json("3"), null);

        Assert.True((await setup.ChangeAsync("es-AR", Field(setup, FormCatalogFieldType.DecimalNumber))).IsSuccess);
        Assert.Equal(0, setup.Writes);

        var same = Catalog(FormCatalogFieldType.Text, Json("A"));
        Assert.True((await same.ChangeAsync("es-AR", Field(same, FormCatalogFieldType.Text))).IsSuccess);
        Assert.Equal(0, same.BatchReads);
    }

    private static async Task<string?[]> ToTextAsync(string culture, FormCatalogFieldType type, params JsonElement?[] values)
    {
        var setup = Catalog(type, values);
        var result = await setup.ChangeAsync(culture, Field(setup, FormCatalogFieldType.Text));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return [.. Enumerable.Range(0, values.Length).Select(index => setup.Value(index)?.GetString())];
    }

    /// <summary>Un catálogo con un campo «dato» del tipo pedido, opcional, y una opción activa por valor, en ese orden.</summary>
    private static Setup Catalog(FormCatalogFieldType type, params JsonElement?[] values)
    {
        var catalog = FormCatalog.CreateManual("Proveedores", null, FormCatalogValueType.Text);
        var created = catalog.ChangeFields([new FormCatalogFieldDraft(null, "dato", "Dato", type, type == FormCatalogFieldType.Catalog ? Zones : null,
            false, false, null, null, null)], name => name);
        Assert.True(created.IsSuccess);
        var fieldId = created.Value[0].Id;
        var items = new List<FormCatalogItem>();
        for (var index = 0; index < values.Length; index++)
        {
            var item = FormCatalogItem.Create(catalog, index, $"C{index}", $"Opción {index}", null, null, isActive: true, position: index).Value;
            if (values[index] is { } value) Assert.True(item.SetFieldValues(catalog, index, new Dictionary<Guid, JsonElement?> { [fieldId] = value }).IsSuccess);
            items.Add(item);
        }

        return new Setup(catalog, fieldId, items);
    }

    /// <summary>El campo guardado, con su identidad y su clave, para cambiarle el tipo o una regla.</summary>
    private static FormCatalogFieldDraft Field(Setup setup, FormCatalogFieldType type) =>
        new(setup.FieldId, "dato", "Dato", type, type == FormCatalogFieldType.Catalog ? Zones : null, false, false, null, null, null);

    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);

    private static (string Key, string Message) Single(Error error)
    {
        var (key, messages) = Assert.Single(Assert.IsType<ValidationError>(error).Errors);
        return (key, Assert.Single(messages));
    }

    private static IReadOnlyList<FormCatalogInvalidRow> Rows(Error error) =>
        Assert.IsAssignableFrom<IReadOnlyList<FormCatalogInvalidRow>>(error.Metadata!["invalidRows"]);

    private static DisplayFormatter Formatter()
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        return new DisplayFormatter(catalog, catalog, catalog, catalog, catalog, new LibPhoneNumberDisplayFormatter(),
            new TimeZoneService(clock), clock);
    }

    /// <summary>El catálogo, sus opciones y lo que lee y escribe la política sobre ellas.</summary>
    private sealed class Setup(FormCatalog catalog, Guid fieldId, List<FormCatalogItem> items)
    {
        private readonly Store _store = new(catalog, items);
        public Guid FieldId { get; } = fieldId;
        public List<FormCatalogItem> Items => items;
        public int Writes => _store.Writes;
        public int BatchReads => _store.BatchReads;

        public JsonElement? Value(int index) => items[index].ReadFieldValues().TryGetValue("dato", out var value) ? value : null;

        /// <summary>La opción cambiada en el pedido con el valor del campo, ya en el tipo nuevo.</summary>
        public FormCatalogItemInput Edit(int index, JsonElement? value) =>
            new(items[index].Id, null, items[index].Code, items[index].Text, null, null, true, new Dictionary<Guid, JsonElement?> { [FieldId] = value });

        /// <summary>La opción dada de baja en el pedido, sin tocar sus valores.</summary>
        public FormCatalogItemInput Deactivate(int index) => new(items[index].Id, null, items[index].Code, items[index].Text, null, null, false);

        /// <summary>Cambia el campo en el catálogo como lo hace el editor y le pasa a la política lo que había antes.</summary>
        public async Task<Result> ChangeAsync(string culture, FormCatalogFieldDraft field, FormCatalogItemChanges? changes = null)
        {
            var before = catalog.ReadFields();
            var changed = catalog.ChangeFields([field], name => name);
            Assert.True(changed.IsSuccess, changed.IsFailure ? changed.Error.Code : null);
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            try
            {
                var formatter = Formatter();
                var policy = new FormCatalogFieldChangePolicy(_store, _store, new FormCatalogFieldValuePolicy(formatter, _store), formatter);
                return await policy.ApplyAsync(catalog, before, changes ?? NoItems, Ct);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
                // Cada prueba vuelve al campo como estaba, para probar otro cambio sobre los mismos valores.
                catalog.ChangeFields([.. before.Where(saved => saved.IsActive).Select(saved => new FormCatalogFieldDraft(saved.Id, saved.Key,
                    saved.Name, saved.Type, saved.SourceCatalogId, saved.Required, saved.Unique, saved.MaxLength, saved.Min, saved.Max))], name => name);
            }
        }
    }

    /// <summary>Las opciones del catálogo y de las fuentes («Zonas», con Norte activa y Sur dada de baja, y «Otro»), como las leen el lector y el repositorio.</summary>
    private sealed class Store(FormCatalog catalog, List<FormCatalogItem> items) : IFormCatalogReader, IFormCatalogRepository
    {
        private readonly FormCatalogStoredItemRow[] _zones =
        [
            new(North, "NOR", "NOR", "Norte", null, null, 0, true),
            new(South, "SUR", "SUR", "Sur", null, null, 1, false),
        ];

        public int BatchReads { get; private set; }
        public int Writes => items.Count(item => item.FieldValuesJson != _original[item.Id]);
        private readonly Dictionary<Guid, string> _original = items.ToDictionary(item => item.Id, item => item.FieldValuesJson);

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take, CancellationToken ct)
        {
            Assert.Equal(catalog.Id, catalogId);
            BatchReads++;
            return Task.FromResult<IReadOnlyList<FormCatalogStoredItemRow>>([.. items.Where(item => afterPosition is null || item.Position > afterPosition)
                .OrderBy(item => item.Position).Take(take)
                .Select(item => new FormCatalogStoredItemRow(item.Id, item.Code, item.CodeNormalized, item.Text, item.Detail, null, item.Position,
                    item.IsActive, item.FieldValuesJson))]);
        }

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FormCatalogStoredItemRow>>(catalogId == Zones ? [.. _zones.Where(row => itemIds.Contains(row.Id))] : []);

        public Task<IReadOnlyList<FormCatalogItem>> ListItemsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FormCatalogItem>>([.. items.Where(item => itemIds.Contains(item.Id))]);

        public Task<PagedResult<FormCatalogStoredRow>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<FormCatalogStoredRow?> FindAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();
        public Task<FormCatalogFieldsRow?> FindFieldsAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<FormCatalogFieldSourceRow>> ListFieldSourcesAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<FormCatalogItemCandidateRow>> ListItemCandidatesAsync(Guid catalogId, IReadOnlyCollection<string> searches,
            int perSearch, CancellationToken ct) => throw new NotSupportedException();
        public Task LockAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct) => throw new NotSupportedException();
        public Task LockWithSourcesAsync(Guid catalogId, IReadOnlyCollection<Guid> requestedSourceIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<FormCatalog?> GetByIdAsync(Guid catalogId, uint expectedVersion, CancellationToken ct) => throw new NotSupportedException();
        Task<IReadOnlyList<FormCatalogItem>> IFormCatalogRepository.ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<FormCatalogItem>> ListActiveItemsByFieldValuesAsync(Guid catalogId, string key, IReadOnlyCollection<string> upperValues,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<FormCatalogItem>> ListAllItemsAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountItemsAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();
        public Task<int?> FindLastPositionAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();
        public void Add(FormCatalog catalog) => throw new NotSupportedException();
        public void Add(FormCatalogItem item) => throw new NotSupportedException();
        public void MarkUpdated(FormCatalog catalog) => throw new NotSupportedException();
        public void Remove(FormCatalogItem item) => throw new NotSupportedException();
    }
}
