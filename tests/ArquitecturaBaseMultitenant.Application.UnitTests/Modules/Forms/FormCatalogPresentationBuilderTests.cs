using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// La presentación de un catálogo (E13, B8): el título y la descripción de cada opción en WhatsApp, con los lugares que tienen
/// contenido unidos con « · »; 30 caracteres de título y 300 de descripción entran y uno más no; lo vacío no cuenta y un título sin
/// contenido se avisa, también con un valor de texto <c>""</c> (lección 3); un campo quitado sale de la presentación; cada lugar se
/// lee en la cultura del pedido, también una opción fuente dada de baja (lección 1), y el detalle cuenta las opciones activas que no
/// entran, leídas en lotes de 500, con los tres primeros ejemplos en orden de posición.
/// </summary>
public sealed class FormCatalogPresentationBuilderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid CatalogId = Guid.NewGuid();
    private static readonly Guid ZonesId = Guid.NewGuid();
    private static readonly Guid North = Guid.NewGuid();
    private static readonly Guid South = Guid.NewGuid();

    private static readonly FormCatalogField Nickname = Field("apodo", FormCatalogFieldType.Text, position: 0);
    private static readonly FormCatalogField Term = Field("plazo", FormCatalogFieldType.WholeNumber, position: 1);
    private static readonly FormCatalogField Since = Field("desde", FormCatalogFieldType.Date, position: 2);
    private static readonly FormCatalogField Vat = Field("iva", FormCatalogFieldType.YesNo, position: 3);
    private static readonly FormCatalogField Zone = Field("zona", FormCatalogFieldType.Catalog, position: 4);

    [Fact]
    public void Thirty_characters_of_title_and_three_hundred_of_description_fit_and_one_more_does_not()
    {
        var text = new string('a', 16);
        var presentation = new FormCatalogPresentation([FormCatalogPresentation.Text, FormCatalogPresentation.Code],
            [FormCatalogPresentation.Code, FormCatalogPresentation.Detail]);

        var fits = FormCatalogPresentationBuilder.Build(presentation, Places(text, new string('B', 11), new string('d', 286)));
        Assert.Equal((30, 300), (fits.Title.Length, fits.Description.Length));
        Assert.Null(fits.Issue);

        var longTitle = FormCatalogPresentationBuilder.Build(presentation, Places(text, new string('B', 12), "d"));
        Assert.Equal(31, longTitle.Title.Length);
        Assert.Equal(FormCatalogPresentationIssue.TitleTooLong, longTitle.Issue);

        var longDescription = FormCatalogPresentationBuilder.Build(presentation, Places(text, new string('B', 11), new string('d', 287)));
        Assert.Equal(301, longDescription.Description.Length);
        Assert.Equal(FormCatalogPresentationIssue.DescriptionTooLong, longDescription.Issue);

        Assert.Equal(FormCatalogPresentationIssue.TitleTooLong,
            FormCatalogPresentationBuilder.Build(presentation, Places(text, new string('B', 12), new string('d', 288))).Issue);
    }

    [Fact]
    public void Places_without_content_do_not_count_and_a_title_without_content_is_empty()
    {
        var presentation = new FormCatalogPresentation([FormCatalogPresentation.Code, FormCatalogPresentation.Detail],
            [FormCatalogPresentation.Detail, FormCatalogPresentation.Text]);

        var empty = FormCatalogPresentationBuilder.Build(presentation, Places("Abrasivos", "  ", null));
        Assert.Equal((string.Empty, "Abrasivos"), (empty.Title, empty.Description));
        Assert.Equal(FormCatalogPresentationIssue.TitleEmpty, empty.Issue);

        var trimmed = FormCatalogPresentationBuilder.Build(presentation, Places("Abrasivos", " ABR ", "   "));
        Assert.Equal(("ABR", "Abrasivos"), (trimmed.Title, trimmed.Description));
        Assert.Null(trimmed.Issue);
    }

    [Theory]
    [InlineData("es-AR", "2.500,5 · 1.500 · 31/03/2026 · Sí · Sur")]
    [InlineData("en-US", "2,500.5 · 1,500 · 03/31/2026 · Yes · Sur")]
    public async Task Each_place_is_read_in_the_request_culture_also_an_inactive_source_option(string culture, string description)
    {
        var store = new Store();
        var layout = FormCatalogPresentationBuilder.Layout(
            new FormCatalogPresentation([FormCatalogPresentation.Text, Place(Nickname)],
                [FormCatalogPresentation.Value, Place(Term), Place(Since), Place(Vat), Place(Zone), FormCatalogPresentation.Detail]),
            [Nickname, Term, Since, Vat, Zone], FormCatalogValueType.DecimalNumber);
        FormCatalogStoredItemRow[] rows =
        [
            Item(0, "Abrasivos", value: "2500.5", fields: new() { ["apodo"] = "", ["plazo"] = "1500", ["desde"] = "2026-03-31", ["iva"] = true, ["zona"] = South.ToString("D") }),
            Item(1, "Bulones", value: "10", fields: new() { ["zona"] = North.ToString("D") }),
        ];

        var built = await Builder(store).BuildAsync(layout, rows, await CultureAsync(culture), Ct);

        Assert.Equal(("Abrasivos", description), (built[0].Title, built[0].Description));
        Assert.Equal(("Bulones", "10 · Norte"), (built[1].Title, built[1].Description));
        Assert.Equal(1, store.SourceReads);
    }

    [Fact]
    public async Task A_removed_field_leaves_the_presentation_and_an_empty_title_goes_back_to_the_text()
    {
        var removed = Nickname with { IsActive = false };

        var layout = FormCatalogPresentationBuilder.Layout(new FormCatalogPresentation([Place(removed)], [Place(removed), Place(Term)]),
            [removed, Term], FormCatalogValueType.Text);

        Assert.Equal([FormCatalogPresentation.Text], layout.Presentation.Title);
        Assert.Equal([Place(Term)], layout.Presentation.Description);
        Assert.Equal([Term.Id], layout.Fields.Keys);
        var built = await Builder(new Store()).BuildAsync(layout,
            [Item(0, "Abrasivos", fields: new() { ["apodo"] = new string('x', 40), ["plazo"] = "30" })], await CultureAsync("es-AR"), Ct);
        Assert.Equal(("Abrasivos", "30"), (built[0].Title, built[0].Description));
        Assert.Null(built[0].Issue);
    }

    [Fact]
    public async Task The_detail_counts_the_active_options_that_do_not_fit_with_the_first_three_examples_in_position_order()
    {
        var store = new Store(new FormCatalogPresentation([Place(Nickname)], [FormCatalogPresentation.Detail]), Nickname);
        var tooLong = new string('t', 31);
        store.Items.Add(Item(0, "Inactiva", isActive: false));
        for (var position = 1; position <= 1203; position++)
        {
            store.Items.Add(position switch
            {
                2 => Item(position, "Aceros", fields: new() { ["apodo"] = tooLong }),
                7 => Item(position, "Herramientas"),
                9 => Item(position, "Cementos", detail: new string('d', 301), fields: new() { ["apodo"] = "Cem" }),
                600 or 1100 => Item(position, "Otra", fields: new() { ["apodo"] = tooLong }),
                _ => Item(position, "Opción", fields: new() { ["apodo"] = "Ok" }),
            });
        }

        var issues = await Builder(store).IssuesAsync(store.Catalog(), store.Presentation, [Nickname], Ct);

        Assert.Equal(5, issues.Count);
        Assert.Equal(
            [
                (store.Items[2].Id, tooLong, FormCatalogPresentationIssue.TitleTooLong),
                (store.Items[7].Id, "Herramientas", FormCatalogPresentationIssue.TitleEmpty),
                (store.Items[9].Id, "Cem", FormCatalogPresentationIssue.DescriptionTooLong),
            ],
            issues.Examples.Select(example => (example.ItemId, example.Text, example.Issue)));
        Assert.Equal([null, 500, 1000], store.Batches);
    }

    [Fact]
    public async Task A_presentation_that_cannot_fail_reads_no_options()
    {
        var store = new Store(FormCatalogPresentation.Default);
        store.Items.Add(Item(0, "Abrasivos"));

        var issues = await Builder(store).IssuesAsync(store.Catalog(), store.Presentation, [], Ct);

        Assert.Equal(FormCatalogPresentationIssues.None, issues);
        Assert.Empty(store.Batches);
    }

    [Fact]
    public async Task Each_option_of_a_page_says_what_does_not_fit()
    {
        var store = new Store(new FormCatalogPresentation([FormCatalogPresentation.Text, Place(Nickname)], []), Nickname);
        var page = new PagedResult<FormCatalogItemRow>(
        [
            Row("Aceros", new Dictionary<Guid, JsonElement> { [Nickname.Id] = Json("\"perfiles laminados en frío\"") }),
            Row("Bulones", new Dictionary<Guid, JsonElement> { [Nickname.Id] = Json("\"\"") }),
            Row("Cementos", FormCatalogItemRow.NoFields),
        ], 1, 25, 3);

        var shown = await Builder(store).WithIssuesAsync(store.Catalog(), page, Ct);

        Assert.Equal([FormCatalogPresentationIssue.TitleTooLong, null, null], shown.Items.Select(row => row.PresentationIssue));
        Assert.Equal(page.Items.Select(row => row.Id), shown.Items.Select(row => row.Id));
    }

    private static Func<string, string?> Places(string text, string code, string? detail) => place => place switch
    {
        FormCatalogPresentation.Text => text,
        FormCatalogPresentation.Code => code,
        FormCatalogPresentation.Detail => detail,
        _ => null,
    };

    private static string Place(FormCatalogField field) => FormCatalogPresentation.Field(field.Id);

    private static FormCatalogField Field(string key, FormCatalogFieldType type, int position) => new(Guid.NewGuid(), key, key, type,
        type == FormCatalogFieldType.Catalog ? ZonesId : null, false, false, null, null, null, position, true);

    private static FormCatalogStoredItemRow Item(int position, string text, string? detail = null, string? value = null,
        bool isActive = true, Dictionary<string, object>? fields = null) =>
        new(Guid.NewGuid(), $"C{position}", $"C{position}", text, detail, value, position, isActive,
            JsonSerializer.Serialize(fields ?? new Dictionary<string, object>()));

    private static FormCatalogItemRow Row(string text, IReadOnlyDictionary<Guid, JsonElement> fields) =>
        new(Guid.NewGuid(), text[..3].ToUpperInvariant(), text, null, null, 0, true, false, fields, null);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static DisplayFormatter Formatter()
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        return new DisplayFormatter(catalog, catalog, catalog, catalog, catalog, new LibPhoneNumberDisplayFormatter(),
            new TimeZoneService(clock), clock);
    }

    private static Task<DisplayFormatContext> CultureAsync(string culture) => Formatter().CreateAsync(culture, TimeZoneInfo.Utc.Id, Ct);

    private static FormCatalogPresentationBuilder Builder(Store store)
    {
        var formatter = Formatter();
        return new FormCatalogPresentationBuilder(store, new FormCatalogFieldValuePolicy(formatter, store), formatter);
    }

    /// <summary>Un catálogo a mano con sus opciones, y «Zonas», la fuente del campo «zona», con «Sur» dada de baja.</summary>
    private sealed class Store(FormCatalogPresentation? presentation = null, params FormCatalogField[] fields) : IFormCatalogReader
    {
        public FormCatalogPresentation Presentation { get; } = presentation ?? FormCatalogPresentation.Default;
        public List<FormCatalogStoredItemRow> Items { get; } = [];
        public List<int?> Batches { get; } = [];
        public int SourceReads { get; private set; }

        public FormCatalogStoredRow Catalog() => new(CatalogId, "Rubros", null, FormCatalogSource.Manual, FormCatalogValueType.Text, null,
            true, Items.Count, Items.Count(item => item.IsActive), new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc), null, 1);

        public Task<FormCatalogPresentationRow?> FindPresentationAsync(Guid catalogId, CancellationToken ct) =>
            Task.FromResult<FormCatalogPresentationRow?>(catalogId == CatalogId
                ? new FormCatalogPresentationRow(FormCatalogField.ToJson(fields), Presentation.ToJson())
                : null);

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListActiveItemBatchAsync(Guid catalogId, int? afterPosition, int take,
            CancellationToken ct)
        {
            Batches.Add(afterPosition);
            return Task.FromResult<IReadOnlyList<FormCatalogStoredItemRow>>([.. Items
                .Where(item => item.IsActive && (afterPosition is null || item.Position > afterPosition))
                .OrderBy(item => item.Position).Take(take)]);
        }

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds,
            CancellationToken ct)
        {
            SourceReads++;
            FormCatalogStoredItemRow[] zones = catalogId == ZonesId
                ? [new(North, "NOR", "NOR", "Norte", null, null, 0, true), new(South, "SUR", "SUR", "Sur", null, null, 1, false)]
                : [];
            return Task.FromResult<IReadOnlyList<FormCatalogStoredItemRow>>([.. zones.Where(zone => itemIds.Contains(zone.Id))]);
        }

        public Task<PagedResult<FormCatalogStoredRow>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<FormCatalogStoredRow?> FindAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();

        public Task<FormCatalogFieldsRow?> FindFieldsAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogFieldSourceRow>> ListFieldSourcesAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogItemCandidateRow>> ListItemCandidatesAsync(Guid catalogId, IReadOnlyCollection<string> searches,
            int perSearch, CancellationToken ct) => throw new NotSupportedException();
    }
}
