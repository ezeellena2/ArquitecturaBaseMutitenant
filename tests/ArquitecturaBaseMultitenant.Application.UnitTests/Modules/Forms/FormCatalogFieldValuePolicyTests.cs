using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// El valor de un campo (E13, B3): lo que escribe una persona se lee con la cultura del pedido y lo que se lee de dos formas
/// («1.500» y «12.345» en español, «1,500» en inglés) no se lee; <c>""</c> es un texto vacío y <c>null</c> limpia, sin romper la
/// lectura (lección 3); un valor nuevo de «De un catálogo» pide una opción activa de su fuente, y el guardado que apunta a una dada
/// de baja se conserva (lección 1). Los mensajes salen en es y en.
/// </summary>
public sealed class FormCatalogFieldValuePolicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Zones = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid North = Guid.NewGuid();
    private static readonly Guid South = Guid.NewGuid();
    private static readonly Guid Foreign = Guid.NewGuid();

    [Theory]
    [InlineData("1.500", "es-AR", FormCatalogFieldType.WholeNumber, null, FormCatalogFieldValueIssue.Ambiguous)]
    [InlineData("12.345", "es-AR", FormCatalogFieldType.DecimalNumber, null, FormCatalogFieldValueIssue.Ambiguous)]
    [InlineData("1.234,5", "es-AR", FormCatalogFieldType.DecimalNumber, "1234.5", null)]
    [InlineData("1500", "es-AR", FormCatalogFieldType.WholeNumber, "1500", null)]
    [InlineData("1.500", "en-US", FormCatalogFieldType.DecimalNumber, "1.5", null)]
    [InlineData("12.345", "en-US", FormCatalogFieldType.DecimalNumber, "12.345", null)]
    [InlineData("1.500", "en-US", FormCatalogFieldType.WholeNumber, null, FormCatalogFieldValueIssue.Invalid)]
    [InlineData("1,500", "en-US", FormCatalogFieldType.WholeNumber, null, FormCatalogFieldValueIssue.Ambiguous)]
    [InlineData("12,345", "en-US", FormCatalogFieldType.DecimalNumber, null, FormCatalogFieldValueIssue.Ambiguous)]
    [InlineData("3/4/2026", "es-AR", FormCatalogFieldType.Date, null, FormCatalogFieldValueIssue.Ambiguous)]
    [InlineData("27/09/2026", "es-AR", FormCatalogFieldType.Date, "2026-09-27", null)]
    [InlineData("09/27/2026", "en-US", FormCatalogFieldType.Date, "2026-09-27", null)]
    [InlineData("doce", "es-AR", FormCatalogFieldType.WholeNumber, null, FormCatalogFieldValueIssue.Invalid)]
    public async Task What_a_person_writes_is_read_with_the_request_culture_and_what_reads_two_ways_is_not_read(string text,
        string culture, FormCatalogFieldType type, string? canonical, FormCatalogFieldValueIssue? issue)
    {
        var read = FormCatalogFieldValuePolicy.Read(Field(type), text, await CultureAsync(culture));

        Assert.Equal(canonical, read.Canonical?.GetString());
        Assert.Equal(issue, read.Issue);
    }

    [Theory]
    [InlineData("es-AR")]
    [InlineData("en-US")]
    public async Task An_empty_text_and_null_do_not_break_the_reading(string culture)
    {
        var context = await CultureAsync(culture);
        var text = Field(FormCatalogFieldType.Text);
        var number = Field(FormCatalogFieldType.WholeNumber);

        Assert.Equal("", text.Check(Json("\"\"")).Canonical?.GetString());
        Assert.Equal(FormCatalogFieldValueIssue.Invalid, number.Check(Json("\"\"")).Issue);
        Assert.True(text.Check(Json("null")).IsEmpty);
        Assert.True(number.Check(default).IsEmpty);
        Assert.Equal("", FormCatalogFieldValuePolicy.Read(text, "", context).Canonical?.GetString());
        Assert.True(FormCatalogFieldValuePolicy.Read(number, "  ", context).IsEmpty);
        Assert.True(FormCatalogFieldValuePolicy.Read(Field(FormCatalogFieldType.Date), "", context).IsEmpty);
        Assert.True(FormCatalogFieldValuePolicy.Read(Field(FormCatalogFieldType.YesNo), "", context).IsEmpty);
    }

    [Theory]
    [InlineData("Sí", "es-AR", true)]
    [InlineData("si", "es-AR", true)]
    [InlineData("NO", "es-AR", false)]
    [InlineData("Yes", "en-US", true)]
    [InlineData("no", "en-US", false)]
    [InlineData("true", "en-US", true)]
    [InlineData("Sí", "en-US", null)]
    [InlineData("Quizás", "es-AR", null)]
    public async Task Yes_and_no_are_read_with_the_words_of_the_request_culture(string text, string culture, bool? expected)
    {
        var read = FormCatalogFieldValuePolicy.Read(Field(FormCatalogFieldType.YesNo), text, await CultureAsync(culture));

        if (expected is { } value) Assert.Equal(value, read.Canonical?.GetBoolean());
        else Assert.Equal(FormCatalogFieldValueIssue.Invalid, read.Issue);
    }

    [Theory]
    [InlineData("es-AR", "Puede tener hasta 30 caracteres.", "Tiene que estar entre 1.000 y 2.500,5.", "Tiene que estar entre 01/01/2026 y 31/12/2026.")]
    [InlineData("en-US", "It can have up to 30 characters.", "It must be between 1,000 and 2,500.5.", "It must be between 01/01/2026 and 12/31/2026.")]
    public async Task The_message_of_a_value_says_its_limit_in_the_request_culture(string culture, string length, string range,
        string dateRange)
    {
        var context = await CultureAsync(culture);
        var text = Field(FormCatalogFieldType.Text) with { MaxLength = 30 };
        var amount = Field(FormCatalogFieldType.DecimalNumber) with { Min = "1000", Max = "2500.5" };
        var day = Field(FormCatalogFieldType.Date) with { Min = "2026-01-01", Max = "2026-12-31" };
        var policy = Policy();

        Assert.Equal(FormCatalogFieldValueIssue.Length, text.Check(Json($"\"{new string('a', 31)}\"")).Issue);
        Assert.Equal(FormCatalogFieldValueIssue.Range, amount.Check(Json("\"2500.51\"")).Issue);
        Assert.Equal(FormCatalogFieldValueIssue.Range, day.Check(Json("\"2025-12-31\"")).Issue);
        Assert.Equal(length, Message(policy.ErrorOf(3, text, FormCatalogFieldValueIssue.Length, context), culture));
        Assert.Equal(range, Message(policy.ErrorOf(3, amount, FormCatalogFieldValueIssue.Range, context), culture));
        Assert.Equal(dateRange, Message(policy.ErrorOf(3, day, FormCatalogFieldValueIssue.Range, context), culture));
    }

    [Theory]
    [InlineData("es-AR", "Escribí un número entero.", "Se lee de dos formas.", "El valor no corresponde a este campo.")]
    [InlineData("en-US", "Enter a whole number.", "It can be read two ways.", "This value doesn't match the field.")]
    public async Task A_value_of_another_type_says_what_to_write_as_in_E12(string culture, string wholeNumber, string ambiguous,
        string other)
    {
        var context = await CultureAsync(culture);
        var number = Field(FormCatalogFieldType.WholeNumber);
        var onlyMinimum = number with { Min = "5" };
        var source = Field(FormCatalogFieldType.Catalog);
        var policy = Policy();

        Assert.Equal(wholeNumber, Message(policy.ErrorOf(0, number, FormCatalogFieldValueIssue.Invalid, context), culture));
        Assert.Equal(ambiguous, Message(policy.ErrorOf(0, number, FormCatalogFieldValueIssue.Ambiguous, context), culture));
        Assert.Equal(other, Message(policy.ErrorOf(0, source, FormCatalogFieldValueIssue.Invalid, context), culture));
        Assert.Equal(other, Message(policy.ErrorOf(0, onlyMinimum, FormCatalogFieldValueIssue.Range, context), culture));
    }

    [Theory]
    [InlineData("es-AR", "El valor no corresponde a este campo.")]
    [InlineData("en-US", "This value doesn't match the field.")]
    public async Task A_new_value_needs_an_active_option_of_its_source_and_a_saved_deactivated_one_is_kept(string culture, string rejected)
    {
        var reader = new Options();
        var policy = Policy(reader);
        var zone = Field(FormCatalogFieldType.Catalog);

        var kept = await policy.CheckOptionsAsync([new(zone, South, South)], Ct);
        var accepted = await policy.CheckOptionsAsync([
            new(zone, North, null),
            new(zone, South, null),
            new(zone, South, South),
            new(zone, Foreign, North),
            new(zone, Guid.NewGuid(), null),
        ], Ct);

        Assert.Equal([true], kept);
        Assert.Equal([true, false, true, false, false], accepted);
        Assert.Equal(1, reader.Reads);
        Assert.Equal(South.ToString("D"), zone.Check(Json($"\"{South}\"")).Canonical?.GetString());
        var error = policy.ErrorOf(4, zone, FormCatalogFieldValueIssue.Invalid, await CultureAsync(culture));
        Assert.Equal([$"items.4.fields.{zone.Id}"], Assert.IsType<ValidationError>(error).Errors.Keys);
        Assert.Equal(rejected, Message(error, culture));
    }

    [Fact]
    public async Task The_culture_is_the_one_of_the_request()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var context = await Policy().CultureAsync(Ct);

            Assert.Equal("en-US", context.Culture);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    private static FormCatalogField Field(FormCatalogFieldType type) =>
        new(Guid.NewGuid(), "dato", "Dato", type, type == FormCatalogFieldType.Catalog ? Zones : null, false, false, null, null, null, 0, true);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static DisplayFormatter Formatter()
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        return new DisplayFormatter(catalog, catalog, catalog, catalog, catalog, new LibPhoneNumberDisplayFormatter(),
            new TimeZoneService(clock), clock);
    }

    private static Task<DisplayFormatContext> CultureAsync(string culture) => Formatter().CreateAsync(culture, TimeZoneInfo.Utc.Id, Ct);

    private static FormCatalogFieldValuePolicy Policy(Options? reader = null) => new(Formatter(), reader ?? new Options());

    /// <summary>El mensaje como lo lee la persona: un código se traduce como lo hace el ProblemDetails; un texto ya viene armado.</summary>
    private static string Message(Error error, string culture)
    {
        Assert.Equal(FormCatalogErrors.FieldValueInvalidCode, error.Code);
        var message = Assert.Single(Assert.Single(Assert.IsType<ValidationError>(error).Errors).Value);
        return new FormsErrorTextSource().Find(message, CultureInfo.GetCultureInfo(culture)) ?? message;
    }

    private sealed class Options : IFormCatalogReader
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds,
            CancellationToken ct)
        {
            Reads++;
            FormCatalogStoredItemRow[] rows = catalogId == Zones
                ? [Row(North, "NOR", "Norte", true), Row(South, "SUR", "Sur", false)]
                : catalogId == Other ? [Row(Foreign, "AFU", "Afuera", true)] : [];
            return Task.FromResult<IReadOnlyList<FormCatalogStoredItemRow>>([.. rows.Where(row => itemIds.Contains(row.Id))]);
        }

        public Task<PagedResult<FormCatalogStoredRow>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<FormCatalogFieldsRow?> FindFieldsAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogFieldSourceRow>> ListFieldSourcesAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<FormCatalogStoredRow?> FindAsync(Guid catalogId, CancellationToken ct) => throw new NotSupportedException();

        public Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogItemCandidateRow>> ListItemCandidatesAsync(Guid catalogId, IReadOnlyCollection<string> searches,
            int perSearch, CancellationToken ct) => throw new NotSupportedException();

        private static FormCatalogStoredItemRow Row(Guid id, string code, string text, bool isActive) =>
            new(id, code, FormCatalogItem.NormalizeCode(code), text, null, null, 0, isActive);
    }
}
