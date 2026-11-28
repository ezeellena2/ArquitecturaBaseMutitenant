using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Forms;

public sealed class FormCatalogItemTests
{
    private static FormCatalogItem Item(FormCatalog catalog, string code, int position, string? value = null) =>
        FormCatalogItem.Create(catalog, 0, code, "Opción " + code, null, value, isActive: true, position).Value;

    [Fact]
    public void Create_keeps_the_texts_trimmed_and_the_code_unique_without_case()
    {
        var catalog = FormCatalog.CreateManual("Talles", null, FormCatalogValueType.Text);

        var item = FormCatalogItem.Create(catalog, 0, "  talle-s  ", "  Chico  ", "  ", "ignorado", isActive: false, 3).Value;
        var other = Item(catalog, "TALLE-S", 4);
        var accented = Item(catalog, "más", 5);

        Assert.Equal((catalog.Id, "talle-s", "Chico", null, 3, false), (item.CatalogId, item.Code, item.Text, item.Detail, item.Position, item.IsActive));
        Assert.Equal(item.CodeNormalized, other.CodeNormalized);
        Assert.Equal(FormCatalogItem.NormalizeCode("MÁS"), accented.CodeNormalized);
        Assert.Null(item.ValueText());
        Assert.Null(item.NumberValue);
        Assert.IsAssignableFrom<ITenantOwned>(item);
        Assert.IsAssignableFrom<IAuditable>(item);
        Assert.False(typeof(IVersioned).IsAssignableFrom(typeof(FormCatalogItem)));
    }

    [Fact]
    public void Texts_over_their_limits_give_the_error_of_their_field()
    {
        var catalog = FormCatalog.CreateManual("Talles", null, FormCatalogValueType.Text);

        var text = FormCatalogItem.Create(catalog, 2, "A", new string('x', FormsTextLimits.OptionText + 1), null, null, true, 0);
        var detail = FormCatalogItem.Create(catalog, 3, "A", "Chico", new string('x', FormsTextLimits.OptionDetail + 1), null, true, 0);
        var fits = FormCatalogItem.Create(catalog, 0, new string('c', FormsTextLimits.OptionCode), new string('x', FormsTextLimits.OptionText),
            new string('x', FormsTextLimits.OptionDetail), null, true, 0);

        Assert.Equal(FormCatalogErrors.ItemTextTooLongCode, text.Error.Code);
        Assert.Equal(["items.2.text"], ((ValidationError)text.Error).Errors.Keys);
        Assert.Equal(["items.3.detail"], ((ValidationError)detail.Error).Errors.Keys);
        Assert.True(fits.IsSuccess);
    }

    [Theory]
    [InlineData(FormCatalogValueType.WholeNumber, "123456789012345678", "123456789012345678")]
    [InlineData(FormCatalogValueType.WholeNumber, "-0042", "-42")]
    [InlineData(FormCatalogValueType.DecimalNumber, "123456789012345678.0123456789", "123456789012345678.0123456789")]
    [InlineData(FormCatalogValueType.DecimalNumber, "1.50000000000000", "1.5")]
    [InlineData(FormCatalogValueType.DecimalNumber, "7", "7")]
    [InlineData(FormCatalogValueType.Date, "2026-03-04", "2026-03-04")]
    public void A_typed_catalog_keeps_the_value_canonical(FormCatalogValueType valueType, string value, string canonical)
    {
        var catalog = FormCatalog.CreateManual("Valores", null, valueType);

        var item = FormCatalogItem.Create(catalog, 0, "A", "Uno", null, value, true, 0).Value;

        Assert.Equal(canonical, item.ValueText());
        Assert.Equal(valueType == FormCatalogValueType.Date, item.DateValue is not null);
        Assert.Equal(valueType != FormCatalogValueType.Date, item.NumberValue is not null);
    }

    [Theory]
    [InlineData(FormCatalogValueType.WholeNumber, "1234567890123456789")]
    [InlineData(FormCatalogValueType.WholeNumber, "1.5")]
    [InlineData(FormCatalogValueType.WholeNumber, "1.500")]
    [InlineData(FormCatalogValueType.WholeNumber, "+3")]
    [InlineData(FormCatalogValueType.DecimalNumber, "1.12345678901")]
    [InlineData(FormCatalogValueType.DecimalNumber, "1234567890123456789.5")]
    [InlineData(FormCatalogValueType.DecimalNumber, "1,5")]
    [InlineData(FormCatalogValueType.DecimalNumber, "1.")]
    [InlineData(FormCatalogValueType.DecimalNumber, ".5")]
    [InlineData(FormCatalogValueType.DecimalNumber, "1e3")]
    [InlineData(FormCatalogValueType.Date, "4/3/2026")]
    [InlineData(FormCatalogValueType.Date, "2026-02-30")]
    public void A_typed_catalog_rejects_a_value_of_another_type(FormCatalogValueType valueType, string value)
    {
        var catalog = FormCatalog.CreateManual("Valores", null, valueType);

        var result = FormCatalogItem.Create(catalog, 4, "A", "Uno", null, value, true, 0);

        Assert.Equal(FormCatalogErrors.ValueInvalidCode, result.Error.Code);
        Assert.Equal(["Forms.Catalog.ValueInvalid." + valueType], ((ValidationError)result.Error).Errors["items.4.value"]);
    }

    [Fact]
    public void A_typed_catalog_requires_the_value_also_on_an_inactive_option_and_when_editing()
    {
        var catalog = FormCatalog.CreateManual("Valores", null, FormCatalogValueType.WholeNumber);

        var missing = FormCatalogItem.Create(catalog, 1, "A", "Uno", null, " ", isActive: false, 0);
        var item = Item(catalog, "B", 0, "5");
        var edited = item.Edit(catalog, 7, "B", "Cinco", null, null);

        Assert.Equal(FormCatalogErrors.ValueRequiredCode, missing.Error.Code);
        Assert.Equal(["items.7.value"], ((ValidationError)edited.Error).Errors.Keys);
        Assert.Equal(("Opción B", "5"), (item.Text, item.ValueText()));
        Assert.True(item.Edit(catalog, 7, "b2", "Seis", "Detalle", "6").IsSuccess);
        Assert.Equal(("b2", "B2", "Seis", "Detalle", "6"), (item.Code, item.CodeNormalized, item.Text, item.Detail, item.ValueText()));
    }

    [Fact]
    public void Swapping_positions_moves_between_neighbors_and_stops_at_the_ends()
    {
        var catalog = FormCatalog.CreateManual("Talles", null, FormCatalogValueType.Text);
        var first = Item(catalog, "S", 0);
        var second = Item(catalog, "M", 1);
        var last = Item(catalog, "L", 2);

        Assert.Equal(FormCatalogErrors.MoveOutOfRange, first.SwapPosition(neighbor: null).Error);
        Assert.Equal(FormCatalogErrors.MoveOutOfRange, last.SwapPosition(neighbor: null).Error);
        Assert.Equal((0, 2), (first.Position, last.Position));

        Assert.True(last.SwapPosition(second).IsSuccess);
        Assert.Equal((0, 2, 1), (first.Position, second.Position, last.Position));
        Assert.Throws<InvalidOperationException>(() => first.SwapPosition(first));
        var otherCatalog = FormCatalog.CreateManual("Colores", null, FormCatalogValueType.Text);
        Assert.Throws<InvalidOperationException>(() => first.SwapPosition(Item(otherCatalog, "R", 1)));
    }

    [Fact]
    public void Options_belong_only_to_a_manual_catalog()
    {
        var members = FormCatalog.CreateMembers("Vendedores", null,
            new FormCatalogFilters(1, [], ExcludeRecipient: false, Detail: null, Sort: null, SampleId: null));
        var catalog = FormCatalog.CreateManual("Talles", null, FormCatalogValueType.Text);
        var item = Item(catalog, "S", 0);

        Assert.Throws<InvalidOperationException>(() => FormCatalogItem.Create(members, 0, "A", "Uno", null, null, true, 0));
        Assert.Throws<InvalidOperationException>(() => item.Edit(FormCatalog.CreateManual("Otro", null, FormCatalogValueType.Text), 0, "S", "Chico", null, null));
        item.Deactivate();
        Assert.False(item.IsActive);
        item.Reactivate();
        Assert.True(item.IsActive);
    }

    private static FormCatalogFieldDraft FieldDraft(string name, FormCatalogFieldType type, int? maxLength = null, string? min = null,
        string? max = null, Guid? source = null) => new(null, null, name, type, source, false, false, maxLength, min, max);

    private static JsonElement? Json<T>(T value) => JsonSerializer.SerializeToElement(value);

    /// <summary>Un catálogo con un campo de cada tipo: CUIT (texto hasta 13), Plazo, Tasa, Alta, Activo, Zona y Notas (texto sin regla).</summary>
    private static (FormCatalog Catalog, IReadOnlyList<FormCatalogField> Fields) WithFields()
    {
        var catalog = FormCatalog.CreateManual("Proveedores", null, FormCatalogValueType.Text);
        var fields = catalog.ChangeFields([
            FieldDraft("CUIT", FormCatalogFieldType.Text, maxLength: 13),
            FieldDraft("Plazo", FormCatalogFieldType.WholeNumber, min: "0", max: "90"),
            FieldDraft("Tasa", FormCatalogFieldType.DecimalNumber),
            FieldDraft("Alta", FormCatalogFieldType.Date, min: "2020-01-01", max: "2030-12-31"),
            FieldDraft("Activo", FormCatalogFieldType.YesNo),
            FieldDraft("Zona", FormCatalogFieldType.Catalog, source: Guid.CreateVersion7()),
            FieldDraft("Notas", FormCatalogFieldType.Text),
        ], name => name).Value;
        return (catalog, fields);
    }

    private static Dictionary<Guid, JsonElement?> Delta(params (FormCatalogField Field, JsonElement? Value)[] values) =>
        values.ToDictionary(pair => pair.Field.Id, pair => pair.Value);

    [Fact]
    public void Field_values_are_kept_canonical_by_key()
    {
        var (catalog, fields) = WithFields();
        var item = Item(catalog, "A", 0);
        var zone = Guid.CreateVersion7();

        var result = item.SetFieldValues(catalog, 4, Delta((fields[0], Json("  20-12345678-9 ")), (fields[1], Json("030")),
            (fields[2], Json("1500.50")), (fields[3], Json("2026-03-01")), (fields[4], Json(true)),
            (fields[5], Json(zone.ToString().ToUpperInvariant()))));

        Assert.True(result.IsSuccess);
        Assert.Equal($"{{\"activo\":true,\"alta\":\"2026-03-01\",\"cuit\":\"20-12345678-9\",\"plazo\":\"30\",\"tasa\":\"1500.5\",\"zona\":\"{zone}\"}}",
            item.FieldValuesJson);
        var values = item.ReadFieldValues();
        Assert.Equal("30", values["plazo"].GetString());
        Assert.True(values["activo"].GetBoolean());
        Assert.Equal(6, FormCatalogItem.FieldValuesFromJson(item.FieldValuesJson).Count);
    }

    [Fact]
    public void Field_values_are_a_delta_where_a_missing_field_is_kept_and_null_clears()
    {
        var (catalog, fields) = WithFields();
        var item = Item(catalog, "A", 0);
        Assert.Equal("{}", item.FieldValuesJson);
        item.SetFieldValues(catalog, 0, Delta((fields[0], Json("20123456789")), (fields[1], Json("30")), (fields[4], Json(false))));

        Assert.True(item.SetFieldValues(catalog, 0, Delta((fields[1], null), (fields[4], JsonSerializer.SerializeToElement<object?>(null)))).IsSuccess);

        Assert.Equal("{\"cuit\":\"20123456789\"}", item.FieldValuesJson);
        var before = item.FieldValuesJson;
        Assert.True(item.SetFieldValues(catalog, 0, Delta((fields[0], Json("20123456789")), (fields[1], null))).IsSuccess);
        Assert.True(item.SetFieldValues(catalog, 0, Delta()).IsSuccess);
        Assert.Same(before, item.FieldValuesJson);
    }

    [Theory]
    [InlineData(0, "true")]
    [InlineData(0, "12")]
    [InlineData(0, "\"12345678901234\"")]
    [InlineData(6, "\"x301\"")]
    [InlineData(1, "\"1.5\"")]
    [InlineData(1, "\"1.500\"")]
    [InlineData(1, "\"1,5\"")]
    [InlineData(1, "30")]
    [InlineData(1, "\"91\"")]
    [InlineData(1, "\"-1\"")]
    [InlineData(1, "\"\"")]
    [InlineData(2, "\"1.12345678901\"")]
    [InlineData(3, "\"01/03/2026\"")]
    [InlineData(3, "\"2031-01-01\"")]
    [InlineData(4, "\"Sí\"")]
    [InlineData(5, "\"zona-norte\"")]
    [InlineData(5, "\"00000000-0000-0000-0000-000000000000\"")]
    public void A_value_that_is_not_of_its_type_or_breaks_its_limit_is_invalid(int field, string json)
    {
        var (catalog, fields) = WithFields();
        var item = Item(catalog, "A", 0);
        using var document = JsonDocument.Parse(json.Replace("x301", new string('x', 301), StringComparison.Ordinal));

        var result = item.SetFieldValues(catalog, 2, Delta((fields[field], document.RootElement.Clone())));

        FieldErrorAssert.Same(FormCatalogErrors.FieldValueInvalid(2, fields[field].Id, FormCatalogErrors.FieldValueInvalidCode), result.Error);
        Assert.Equal("{}", item.FieldValuesJson);
    }

    [Fact]
    public void An_empty_text_is_a_value_and_a_text_without_a_rule_takes_up_to_300()
    {
        var (catalog, fields) = WithFields();
        var item = Item(catalog, "A", 0);

        Assert.True(item.SetFieldValues(catalog, 0, Delta((fields[0], Json("")), (fields[6], Json(new string('x', 300))))).IsSuccess);

        Assert.Equal("", item.ReadFieldValues()["cuit"].GetString());
        Assert.Equal(300, item.ReadFieldValues()["notas"].GetString()!.Length);
    }

    [Fact]
    public void A_value_of_an_unknown_or_removed_field_is_invalid_and_the_removed_values_are_kept()
    {
        var (catalog, fields) = WithFields();
        var item = Item(catalog, "A", 0);
        item.SetFieldValues(catalog, 0, Delta((fields[0], Json("20123456789")), (fields[1], Json("30"))));
        catalog.ChangeFields([.. fields.Skip(1).Select(field => new FormCatalogFieldDraft(field.Id, null, field.Name, field.Type,
            field.SourceCatalogId, false, false, field.MaxLength, field.Min, field.Max))], name => name);
        var unknown = Guid.CreateVersion7();

        FieldErrorAssert.Same(FormCatalogErrors.FieldValueInvalid(1, fields[0].Id, FormCatalogErrors.FieldValueInvalidCode),
            item.SetFieldValues(catalog, 1, Delta((fields[0], Json("1")))).Error);
        FieldErrorAssert.Same(FormCatalogErrors.FieldValueInvalid(1, unknown, FormCatalogErrors.FieldValueInvalidCode),
            item.SetFieldValues(catalog, 1, new Dictionary<Guid, JsonElement?> { [unknown] = Json("1") }).Error);
        Assert.True(item.SetFieldValues(catalog, 1, Delta((fields[1], Json("45")))).IsSuccess);

        Assert.Equal("{\"cuit\":\"20123456789\",\"plazo\":\"45\"}", item.FieldValuesJson);
    }

    [Fact]
    public void A_required_field_needs_a_value_only_on_an_active_option()
    {
        var (catalog, fields) = WithFields();
        var drafts = fields.Select(field => new FormCatalogFieldDraft(field.Id, null, field.Name, field.Type, field.SourceCatalogId,
            Required: field.Key is "cuit" or "plazo", false, field.MaxLength, field.Min, field.Max)).ToArray();
        var active = Item(catalog, "A", 0);
        var inactive = FormCatalogItem.Create(catalog, 1, "B", "Opción B", null, null, isActive: false, 1).Value;
        active.SetFieldValues(catalog, 0, Delta((fields[0], Json("20123456789")), (fields[1], Json("30"))));
        Assert.True(catalog.ChangeFields(drafts, name => name).IsSuccess);

        FieldErrorAssert.Same(FormCatalogErrors.FieldValueRequired(3, fields[1].Id), active.SetFieldValues(catalog, 3, Delta((fields[1], null))).Error);
        FieldErrorAssert.Same(FormCatalogErrors.FieldValueRequired(3, fields[0].Id), active.SetFieldValues(catalog, 3, Delta((fields[0], Json("  ")))).Error);
        Assert.Equal("{\"cuit\":\"20123456789\",\"plazo\":\"30\"}", active.FieldValuesJson);
        Assert.True(inactive.SetFieldValues(catalog, 1, Delta((fields[4], Json(true)))).IsSuccess);

        inactive.Reactivate();
        FieldErrorAssert.Same(FormCatalogErrors.FieldValueRequired(1, fields[0].Id), inactive.SetFieldValues(catalog, 1, Delta()).Error);
    }

    [Fact]
    public void Field_values_belong_to_an_option_of_the_same_catalog()
    {
        var (catalog, _) = WithFields();
        var other = FormCatalog.CreateManual("Otro", null, FormCatalogValueType.Text);
        var item = Item(catalog, "A", 0);

        Assert.Throws<InvalidOperationException>(() => item.SetFieldValues(other, 0, Delta()));
    }
}
