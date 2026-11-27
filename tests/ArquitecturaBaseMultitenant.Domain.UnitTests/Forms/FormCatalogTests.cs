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
}
