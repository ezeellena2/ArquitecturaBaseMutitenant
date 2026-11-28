using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Forms;

public sealed class FormCatalogFieldTests
{
    private static string KeyTakenBy(string name) => "taken by " + name;

    private static FormCatalogFieldDraft New(string name, FormCatalogFieldType type = FormCatalogFieldType.Text, string? key = null,
        bool required = false, bool unique = false, int? maxLength = null, string? min = null, string? max = null, Guid? source = null) =>
        new(null, key, name, type, source, required, unique, maxLength, min, max);

    private static FormCatalogFieldDraft Saved(FormCatalogField field, string? key = null, bool? required = null, string? name = null) =>
        new(field.Id, key, name ?? field.Name, field.Type, field.SourceCatalogId, required ?? field.Required, field.Unique, field.MaxLength,
            field.Min, field.Max);

    private static IReadOnlyList<FormCatalogField> Change(FormCatalog catalog, params FormCatalogFieldDraft[] drafts)
    {
        var result = catalog.ChangeFields(drafts, KeyTakenBy);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private static FormCatalog Manual() => FormCatalog.CreateManual("Proveedores", null, FormCatalogValueType.Text);

    [Theory]
    [InlineData("CUIT", "cuit")]
    [InlineData("  Plazo de pago  ", "plazo_de_pago")]
    [InlineData("Año fiscal", "ano_fiscal")]
    [InlineData("Teléfono (fijo)", "telefono_fijo")]
    [InlineData("2024 · Ventas", "campo_2024_ventas")]
    [InlineData("¿?", "campo")]
    public void The_key_is_born_from_the_name_in_lowercase_and_without_accents(string name, string key)
    {
        Assert.Equal(key, FormCatalogField.KeyFromName(name));
        Assert.True(FormCatalogField.IsValidKey(key));
    }

    [Fact]
    public void A_long_name_gives_a_key_within_its_limit()
    {
        var key = FormCatalogField.KeyFromName(new string('a', 30) + " " + new string('b', 30));

        Assert.Equal(FormsTextLimits.FieldKey, key.Length);
        Assert.True(FormCatalogField.IsValidKey(key));
        Assert.False(FormCatalogField.IsValidKey("Cuit"));
        Assert.False(FormCatalogField.IsValidKey("1cuit"));
        Assert.False(FormCatalogField.IsValidKey("cuit-afip"));
        Assert.False(FormCatalogField.IsValidKey(new string('a', FormsTextLimits.FieldKey + 1)));
    }

    [Fact]
    public void The_key_can_be_written_until_saving_and_then_is_fixed()
    {
        var catalog = Manual();
        var cuit = Change(catalog, New("CUIT", key: "cuit_afip"))[0];

        Assert.Equal("cuit_afip", cuit.Key);
        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(0), catalog.ChangeFields([Saved(cuit, key: "cuit")], KeyTakenBy).Error);

        var renamed = Change(catalog, Saved(cuit, name: "CUIT del proveedor"))[0];
        var sameKey = Change(catalog, Saved(renamed, key: "cuit_afip"))[0];
        Assert.Equal((cuit.Id, "cuit_afip", "CUIT del proveedor"), (renamed.Id, renamed.Key, renamed.Name));
        Assert.Equal("cuit_afip", sameKey.Key);
    }

    [Fact]
    public void A_new_key_that_is_not_valid_is_rejected()
    {
        var catalog = Manual();

        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(1), catalog.ChangeFields([New("Zona"), New("CUIT", key: "CUIT")], KeyTakenBy).Error);
        Assert.Equal("[]", catalog.FieldsJson);
    }

    [Fact]
    public void A_reserved_key_or_one_of_a_removed_field_gets_a_number()
    {
        var catalog = Manual();
        var fields = Change(catalog, New("Code"), New("Detalle", key: "detail"), New("CUIT"));
        Assert.Equal(["code_2", "detail_2", "cuit"], fields.Select(field => field.Key));

        Change(catalog, Saved(fields[0]), Saved(fields[1]));
        var again = Change(catalog, Saved(fields[0]), Saved(fields[1]), New("Cuit"));
        Assert.Equal("cuit_2", again[2].Key);

        Change(catalog, Saved(fields[0]), Saved(fields[1]));
        Assert.Equal("cuit_3", Change(catalog, New("cuit"))[0].Key);
        Assert.Equal(["text", "code", "value", "detail"], FormCatalogField.ReservedKeys);
    }

    [Fact]
    public void Two_active_fields_with_the_same_key_give_the_error_on_the_new_one_naming_the_other()
    {
        var catalog = Manual();

        FieldErrorAssert.Same(FormCatalogErrors.FieldKeyDuplicate(1, "taken by Cuit"),
            catalog.ChangeFields([New("Cuit"), New("CUIT")], KeyTakenBy).Error);

        var saved = Change(catalog, New("Zona"), New("CUIT"));
        FieldErrorAssert.Same(FormCatalogErrors.FieldKeyDuplicate(0, "taken by CUIT"),
            catalog.ChangeFields([New("Cuit"), Saved(saved[0]), Saved(saved[1])], KeyTakenBy).Error);
    }

    [Fact]
    public void Twenty_active_fields_count_only_the_active_ones()
    {
        var catalog = Manual();
        var twentyOne = Enumerable.Range(1, FormCatalogField.MaxActive + 1).Select(number => New("Campo " + number)).ToArray();
        FieldErrorAssert.Same(FormCatalogErrors.TooManyFields, catalog.ChangeFields(twentyOne, KeyTakenBy).Error);

        var twenty = Change(catalog, twentyOne[..FormCatalogField.MaxActive]);
        var swapped = Change(catalog, [.. twenty.Skip(1).Select(field => Saved(field)), New("Otro")]);

        Assert.Equal(FormCatalogField.MaxActive, swapped.Count);
        Assert.Equal(FormCatalogField.MaxActive + 1, catalog.ReadFields().Count);
        Assert.False(catalog.ReadFields().Single(field => field.Id == twenty[0].Id).IsActive);
    }

    [Fact]
    public void The_stored_fields_with_the_removed_ones_have_their_own_limit()
    {
        var catalog = Manual();
        for (var round = 0; round < FormCatalogField.MaxStored / FormCatalogField.MaxActive; round++)
            Change(catalog, [.. Enumerable.Range(1, FormCatalogField.MaxActive).Select(number => New($"Ronda {round} campo {number}"))]);

        Assert.Equal(FormCatalogField.MaxStored, catalog.ReadFields().Count);
        FieldErrorAssert.Same(FormCatalogErrors.TooManyFields, catalog.ChangeFields([New("Uno más")], KeyTakenBy).Error);
        Assert.Empty(Change(catalog));
    }

    [Theory]
    [InlineData(FormCatalogFieldType.Text, null, null, null, false, true)]
    [InlineData(FormCatalogFieldType.Text, 1, null, null, false, true)]
    [InlineData(FormCatalogFieldType.Text, FormsTextLimits.FieldTextLength, null, null, false, true)]
    [InlineData(FormCatalogFieldType.Text, 0, null, null, false, false)]
    [InlineData(FormCatalogFieldType.Text, FormsTextLimits.FieldTextLength + 1, null, null, false, false)]
    [InlineData(FormCatalogFieldType.Text, null, "1", null, false, false)]
    [InlineData(FormCatalogFieldType.Text, null, null, null, true, false)]
    [InlineData(FormCatalogFieldType.WholeNumber, null, "-5", "100", false, true)]
    [InlineData(FormCatalogFieldType.WholeNumber, null, "1.5", null, false, false)]
    [InlineData(FormCatalogFieldType.WholeNumber, null, "10", "5", false, false)]
    [InlineData(FormCatalogFieldType.WholeNumber, 10, null, null, false, false)]
    [InlineData(FormCatalogFieldType.WholeNumber, null, "1.500", null, false, false)]
    [InlineData(FormCatalogFieldType.DecimalNumber, null, "0.5", "2.25", false, true)]
    [InlineData(FormCatalogFieldType.DecimalNumber, null, "1,5", null, false, false)]
    [InlineData(FormCatalogFieldType.DecimalNumber, null, null, null, true, false)]
    [InlineData(FormCatalogFieldType.Date, null, "2026-01-01", "2026-12-31", false, true)]
    [InlineData(FormCatalogFieldType.Date, null, "2026-12-31", "2026-01-01", false, false)]
    [InlineData(FormCatalogFieldType.Date, null, "01/01/2026", null, false, false)]
    [InlineData(FormCatalogFieldType.YesNo, null, null, null, false, true)]
    [InlineData(FormCatalogFieldType.YesNo, 5, null, null, false, false)]
    [InlineData(FormCatalogFieldType.YesNo, null, "1", null, false, false)]
    [InlineData(FormCatalogFieldType.YesNo, null, null, null, true, false)]
    [InlineData(FormCatalogFieldType.Catalog, null, null, null, true, true)]
    [InlineData(FormCatalogFieldType.Catalog, null, null, null, false, false)]
    [InlineData(FormCatalogFieldType.Catalog, 20, null, null, true, false)]
    [InlineData(FormCatalogFieldType.Catalog, null, null, "3", true, false)]
    [InlineData((FormCatalogFieldType)99, null, null, null, false, false)]
    public void The_rules_depend_on_the_type(FormCatalogFieldType type, int? maxLength, string? min, string? max, bool withSource, bool valid)
    {
        var catalog = Manual();
        var draft = New("Dato", type, maxLength: maxLength, min: min, max: max, source: withSource ? Guid.CreateVersion7() : null);

        var result = catalog.ChangeFields([New("Primero"), draft], KeyTakenBy);

        Assert.Equal(valid, result.IsSuccess);
        if (!valid) FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(1), result.Error);
    }

    [Fact]
    public void Limits_are_kept_canonical_and_unique_applies_to_every_type()
    {
        var catalog = Manual();
        var fields = Change(catalog,
            New("Plazo", FormCatalogFieldType.DecimalNumber, unique: true, min: "0.50", max: "002.250"),
            New("Alta", FormCatalogFieldType.Date, min: "2026-01-01"),
            New("Activo", FormCatalogFieldType.YesNo, unique: true),
            New("Nombre", maxLength: 50, min: " ", max: ""));

        Assert.Equal(("0.5", "2.25", true), (fields[0].Min, fields[0].Max, fields[0].Unique));
        Assert.Equal(("2026-01-01", (string?)null), (fields[1].Min, fields[1].Max));
        Assert.True(fields[2].Unique);
        Assert.Equal((50, (string?)null, (string?)null), (fields[3].MaxLength, fields[3].Min, fields[3].Max));
    }

    [Fact]
    public void A_catalog_field_cannot_take_its_values_from_its_own_catalog()
    {
        var catalog = Manual();

        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(0),
            catalog.ChangeFields([New("Zona", FormCatalogFieldType.Catalog, source: catalog.Id)], KeyTakenBy).Error);
    }

    [Fact]
    public void A_new_field_is_not_born_required_and_a_saved_one_can_become_required()
    {
        var catalog = Manual();

        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(0), catalog.ChangeFields([New("CUIT", required: true)], KeyTakenBy).Error);

        var cuit = Change(catalog, New("CUIT"))[0];
        Assert.False(cuit.Required);
        Assert.True(Change(catalog, Saved(cuit, required: true))[0].Required);
    }

    [Fact]
    public void An_unknown_removed_or_repeated_field_is_invalid_and_a_removed_one_is_not_reactivated()
    {
        var catalog = Manual();
        var fields = Change(catalog, New("CUIT"), New("Zona"));
        Change(catalog, Saved(fields[1]));

        var unknown = new FormCatalogFieldDraft(Guid.CreateVersion7(), null, "Otro", FormCatalogFieldType.Text, null, false, false, null, null, null);
        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(1), catalog.ChangeFields([Saved(fields[1]), unknown], KeyTakenBy).Error);
        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(1), catalog.ChangeFields([Saved(fields[1]), Saved(fields[0])], KeyTakenBy).Error);
        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(1), catalog.ChangeFields([Saved(fields[1]), Saved(fields[1])], KeyTakenBy).Error);
    }

    [Fact]
    public void Fields_belong_only_to_a_manual_catalog()
    {
        var members = FormCatalog.CreateMembers("Vendedores", null,
            new FormCatalogFilters(1, [], ExcludeRecipient: false, null, null, SampleId: null));

        FieldErrorAssert.Same(FormCatalogErrors.FieldInvalid(0), members.ChangeFields([New("CUIT")], KeyTakenBy).Error);
        Assert.Empty(Change(members));
        Assert.Equal("[]", members.FieldsJson);
    }

    [Fact]
    public void The_fields_are_stored_in_their_order_with_the_removed_ones_and_read_back()
    {
        var catalog = Manual();
        var source = Guid.CreateVersion7();
        var fields = Change(catalog, New("CUIT", maxLength: 13), New("Zona", FormCatalogFieldType.Catalog, source: source),
            New("Plazo", FormCatalogFieldType.WholeNumber, min: "0", max: "90"));

        var reordered = Change(catalog, Saved(fields[2]), Saved(fields[0]));
        var stored = catalog.ReadFields();

        Assert.Equal([fields[2].Id, fields[0].Id], reordered.Select(field => field.Id));
        Assert.Equal([fields[2].Id, fields[0].Id, fields[1].Id], stored.Select(field => field.Id));
        Assert.Equal([0, 1, 2], stored.Select(field => field.Position));
        Assert.Equal([true, true, false], stored.Select(field => field.IsActive));
        Assert.Equal((FormCatalogFieldType.Catalog, source), (stored[2].Type, stored[2].SourceCatalogId));
        Assert.StartsWith($"[{{\"id\":\"{fields[2].Id}\",\"key\":\"plazo\",\"name\":\"Plazo\",\"type\":\"WholeNumber\",\"required\":false,",
            catalog.FieldsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("maxLength\":null", catalog.FieldsJson, StringComparison.Ordinal);
        Assert.Equal(stored, FormCatalogField.ListFromJson(catalog.FieldsJson));
    }

    [Fact]
    public void Saving_the_same_fields_does_not_change_the_column()
    {
        var catalog = Manual();
        var fields = Change(catalog, New("CUIT"), New("Plazo", FormCatalogFieldType.DecimalNumber, min: "1"));
        var before = catalog.FieldsJson;

        Change(catalog, Saved(fields[0]), Saved(fields[1]) with { Min = "1.000", Key = "plazo" });

        Assert.Same(before, catalog.FieldsJson);
    }
}

/// <summary>Compara dos errores por su código, su tipo y sus campos con sus textos: un <see cref="ValidationError"/> trae su diccionario.</summary>
internal static class FieldErrorAssert
{
    public static void Same(Error expected, Error actual)
    {
        Assert.Equal((expected.Code, expected.Type), (actual.Code, actual.Type));
        if (expected is not ValidationError fields) return;
        var actualFields = Assert.IsType<ValidationError>(actual).Errors;
        Assert.Equal(fields.Errors.Select(pair => (pair.Key, string.Join('|', pair.Value))),
            actualFields.Select(pair => (pair.Key, string.Join('|', pair.Value))));
    }
}
