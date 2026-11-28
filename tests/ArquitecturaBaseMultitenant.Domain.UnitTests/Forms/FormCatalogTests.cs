using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Forms;

public sealed class FormCatalogTests
{
    private static FormCatalogFilters Filters(int sourceVersion = 1) =>
        new(sourceVersion, [new FormCatalogCondition("role", "hasAny", ["r1"])], ExcludeRecipient: true, "companies",
            new FormCatalogFilterSort("name", Descending: false), SampleId: Guid.CreateVersion7());

    [Fact]
    public void Source_and_value_type_are_fixed_and_a_filtered_catalog_is_always_text()
    {
        var manual = FormCatalog.CreateManual("  Talles  ", "  ", FormCatalogValueType.DecimalNumber);
        var members = FormCatalog.CreateMembers("Vendedores", "Del local", Filters());
        var contacts = FormCatalog.CreateContacts("Clientes VIP", null, Filters());

        Assert.Equal(("Talles", null, FormCatalogSource.Manual, FormCatalogValueType.DecimalNumber),
            (manual.Name, manual.Description, manual.Source, manual.ValueType));
        Assert.Null(manual.Filters);
        Assert.Equal((FormCatalogSource.Members, FormCatalogValueType.Text), (members.Source, members.ValueType));
        Assert.Equal((FormCatalogSource.Contacts, FormCatalogValueType.Text), (contacts.Source, contacts.ValueType));
        Assert.True(manual.IsActive);
        Assert.IsAssignableFrom<ITenantOwned>(manual);
        Assert.IsAssignableFrom<IVersioned>(manual);
        Assert.IsAssignableFrom<IAuditable>(manual);

        manual.Rename("Talles de remera", "Para el uniforme");
        Assert.Equal((FormCatalogSource.Manual, FormCatalogValueType.DecimalNumber), (manual.Source, manual.ValueType));
        Assert.Equal(("Talles de remera", "Para el uniforme"), (manual.Name, manual.Description));
    }

    [Fact]
    public void Filters_belong_only_to_a_filtered_catalog_and_options_only_to_an_active_manual_one()
    {
        var manual = FormCatalog.CreateManual("Talles", null, FormCatalogValueType.Text);
        var members = FormCatalog.CreateMembers("Vendedores", null, Filters());

        Assert.Equal(FormCatalogErrors.WrongSource, manual.ChangeFilters(Filters()).Error);
        Assert.Null(manual.Filters);
        Assert.True(members.ChangeFilters(Filters(sourceVersion: 2)).IsSuccess);
        Assert.Equal(2, members.ReadFilters()!.SourceVersion);

        Assert.Equal(FormCatalogErrors.WrongSource, members.EnsureItemsCanChange().Error);
        Assert.True(manual.EnsureItemsCanChange().IsSuccess);
        manual.Deactivate();
        Assert.Equal(FormCatalogErrors.Inactive, manual.EnsureItemsCanChange().Error);
        manual.Reactivate();
        Assert.True(manual.EnsureItemsCanChange().IsSuccess);
    }

    [Fact]
    public void Filters_are_stored_as_format_1_json_and_read_back()
    {
        var filters = Filters();
        var catalog = FormCatalog.CreateMembers("Vendedores", null, filters);

        Assert.StartsWith("{\"format\":1,\"sourceVersion\":1,", catalog.Filters, StringComparison.Ordinal);
        var read = catalog.ReadFilters()!;
        Assert.Equal((filters.SourceVersion, filters.ExcludeRecipient, filters.Detail, filters.Sort, filters.SampleId),
            (read.SourceVersion, read.ExcludeRecipient, read.Detail, read.Sort, read.SampleId));
        var condition = Assert.Single(read.Conditions);
        Assert.Equal(("role", "hasAny"), (condition.Column, condition.Operator));
        Assert.Equal(["r1"], condition.Values);
    }

    [Fact]
    public void A_manual_catalog_without_active_options_cannot_be_used()
    {
        var catalog = FormCatalog.CreateManual("Talles", null, FormCatalogValueType.Text);

        Assert.Equal(FormCatalogUnusableReason.NoActiveOptions, Reason(catalog, activeItems: 0));
        Assert.Null(Reason(catalog, activeItems: 1));
        catalog.Deactivate();
        Assert.Null(Reason(catalog, activeItems: 0));
    }

    [Fact]
    public void A_members_catalog_cannot_be_used_with_an_old_source_or_a_missing_role_or_company()
    {
        var catalog = FormCatalog.CreateMembers("Vendedores", null, Filters(sourceVersion: 1));

        Assert.Null(Reason(catalog));
        Assert.Equal(FormCatalogUnusableReason.SourceOutdated, Reason(catalog, currentSourceVersion: 2, roleMissing: true));
        Assert.Equal(FormCatalogUnusableReason.RoleMissing, Reason(catalog, roleMissing: true, companyMissing: true));
        Assert.Equal(FormCatalogUnusableReason.CompanyMissing, Reason(catalog, companyMissing: true));
        Assert.Null(Reason(catalog, channelAvailable: false));
    }

    [Fact]
    public void A_contacts_catalog_cannot_be_used_without_the_channel_or_with_an_old_source()
    {
        var catalog = FormCatalog.CreateContacts("Clientes VIP", null, Filters(sourceVersion: 1));

        Assert.Null(Reason(catalog));
        Assert.Equal(FormCatalogUnusableReason.ChannelUnavailable, Reason(catalog, currentSourceVersion: 2, channelAvailable: false));
        Assert.Equal(FormCatalogUnusableReason.SourceOutdated, Reason(catalog, currentSourceVersion: 2));
        Assert.Null(Reason(catalog, roleMissing: true, companyMissing: true));
        catalog.Deactivate();
        Assert.Null(Reason(catalog, channelAvailable: false));
    }

    private static FormCatalogUnusableReason? Reason(FormCatalog catalog, int activeItems = 0, int currentSourceVersion = 1,
        bool channelAvailable = true, bool roleMissing = false, bool companyMissing = false) =>
        catalog.UnusableReason(activeItems, currentSourceVersion, channelAvailable, roleMissing, companyMissing);

    private static FormCatalogFieldDraft NewField(string name) =>
        new(null, null, name, FormCatalogFieldType.Text, null, false, false, null, null, null);

    private static FormCatalogFieldDraft SavedField(FormCatalogField field) =>
        new(field.Id, field.Key, field.Name, field.Type, field.SourceCatalogId, field.Required, field.Unique, field.MaxLength, field.Min, field.Max);

    private static IReadOnlyList<FormCatalogField> WithFields(FormCatalog catalog, params FormCatalogFieldDraft[] drafts) =>
        catalog.ChangeFields(drafts, name => name).Value;

    [Fact]
    public void A_new_catalog_has_no_fields_and_shows_the_text_as_title_and_the_detail_as_description()
    {
        var manual = FormCatalog.CreateManual("Zonas", null, FormCatalogValueType.Text);
        var members = FormCatalog.CreateMembers("Vendedores", null, Filters());

        Assert.Equal(("[]", "{\"title\":[\"text\"],\"description\":[\"detail\"]}"), (manual.FieldsJson, manual.Presentation));
        Assert.Equal(("[]", manual.Presentation), (members.FieldsJson, members.Presentation));
        Assert.Empty(manual.ReadFields());
        Assert.Equal([FormCatalogPresentation.Text], manual.ReadPresentation().Title);
        Assert.Equal([FormCatalogPresentation.Detail], manual.ReadPresentation().Description);
    }

    [Fact]
    public void The_presentation_takes_the_places_of_the_catalog_and_its_active_fields()
    {
        var catalog = FormCatalog.CreateManual("Proveedores", null, FormCatalogValueType.WholeNumber);
        var fields = WithFields(catalog, NewField("CUIT"), NewField("Zona"));
        var cuit = FormCatalogPresentation.Field(fields[0].Id);

        var result = catalog.ChangePresentation(new([FormCatalogPresentation.Text, FormCatalogPresentation.Code],
            [FormCatalogPresentation.Value, $"field:{fields[0].Id.ToString().ToUpperInvariant()}", FormCatalogPresentation.Detail]));

        Assert.True(result.IsSuccess);
        Assert.Equal($"{{\"title\":[\"text\",\"code\"],\"description\":[\"value\",\"{cuit}\",\"detail\"]}}", catalog.Presentation);
        Assert.Equal(fields[0].Id, FormCatalogPresentation.FieldIdOf(cuit));
        Assert.Null(FormCatalogPresentation.FieldIdOf(FormCatalogPresentation.Text));
        var before = catalog.Presentation;
        Assert.True(catalog.ChangePresentation(catalog.ReadPresentation()).IsSuccess);
        Assert.Same(before, catalog.Presentation);
    }

    [Theory]
    [InlineData("", "detail")]
    [InlineData("text,text", "")]
    [InlineData("text", "detail,detail")]
    [InlineData("text", "value")]
    [InlineData("text", "foo")]
    [InlineData("Text", "")]
    [InlineData("text", "field:")]
    [InlineData("text", "field:00000000-0000-0000-0000-000000000000")]
    [InlineData("text", "field:removed")]
    [InlineData("text", "field:unknown")]
    public void A_place_that_does_not_exist_is_repeated_or_a_title_without_places_is_invalid(string title, string description)
    {
        var catalog = FormCatalog.CreateManual("Proveedores", null, FormCatalogValueType.Text);
        var fields = WithFields(catalog, NewField("CUIT"), NewField("Zona"));
        WithFields(catalog, SavedField(fields[0]));
        string[] Places(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(place => place
            .Replace("removed", fields[1].Id.ToString(), StringComparison.Ordinal)
            .Replace("unknown", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)).ToArray();
        var before = catalog.Presentation;

        var result = catalog.ChangePresentation(new(Places(title), Places(description)));

        FieldErrorAssert.Same(FormCatalogErrors.PresentationInvalid, result.Error);
        Assert.Same(before, catalog.Presentation);
    }

    [Fact]
    public void The_presentation_belongs_only_to_a_manual_catalog_and_the_description_can_be_empty()
    {
        var members = FormCatalog.CreateMembers("Vendedores", null, Filters());
        var manual = FormCatalog.CreateManual("Zonas", null, FormCatalogValueType.Text);

        FieldErrorAssert.Same(FormCatalogErrors.PresentationInvalid, members.ChangePresentation(new([FormCatalogPresentation.Text], [])).Error);
        Assert.True(manual.ChangePresentation(new([FormCatalogPresentation.Code], [])).IsSuccess);
        Assert.Equal("{\"title\":[\"code\"],\"description\":[]}", manual.Presentation);
    }

    [Fact]
    public void Removing_a_field_takes_it_out_of_the_presentation_and_an_empty_title_goes_back_to_the_text()
    {
        var catalog = FormCatalog.CreateManual("Proveedores", null, FormCatalogValueType.Text);
        var fields = WithFields(catalog, NewField("CUIT"), NewField("Zona"));
        var cuit = FormCatalogPresentation.Field(fields[0].Id);
        var zone = FormCatalogPresentation.Field(fields[1].Id);
        Assert.True(catalog.ChangePresentation(new([cuit], [zone, cuit, FormCatalogPresentation.Detail])).IsSuccess);

        WithFields(catalog, SavedField(fields[1]));

        Assert.Equal($"{{\"title\":[\"text\"],\"description\":[\"{zone}\",\"detail\"]}}", catalog.Presentation);
        WithFields(catalog);
        Assert.Equal("{\"title\":[\"text\"],\"description\":[\"detail\"]}", catalog.Presentation);
    }
}
