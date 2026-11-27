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
        Assert.Equal(["items[2].text"], ((ValidationError)text.Error).Errors.Keys);
        Assert.Equal(["items[3].detail"], ((ValidationError)detail.Error).Errors.Keys);
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
        Assert.Equal(["Forms.Catalog.ValueInvalid." + valueType], ((ValidationError)result.Error).Errors["items[4].value"]);
    }

    [Fact]
    public void A_typed_catalog_requires_the_value_also_on_an_inactive_option_and_when_editing()
    {
        var catalog = FormCatalog.CreateManual("Valores", null, FormCatalogValueType.WholeNumber);

        var missing = FormCatalogItem.Create(catalog, 1, "A", "Uno", null, " ", isActive: false, 0);
        var item = Item(catalog, "B", 0, "5");
        var edited = item.Edit(catalog, 7, "B", "Cinco", null, null);

        Assert.Equal(FormCatalogErrors.ValueRequiredCode, missing.Error.Code);
        Assert.Equal(["items[7].value"], ((ValidationError)edited.Error).Errors.Keys);
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
}
