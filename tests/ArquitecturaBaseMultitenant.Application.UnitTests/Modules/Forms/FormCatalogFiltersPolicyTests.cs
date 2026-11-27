using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.StructuredData;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using FormCatalogCondition = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogCondition;
using FormCatalogFilters = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogFilters;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// Los filtros contra su fuente (C2, C5 y C12 de la E12): cada condición usa una columna y un operador de la fuente, con valores
/// que existen en la organización; uno de miembros con condiciones pide la persona de ejemplo, y una de otra organización no
/// sirve. Al leer, sin el canal un catálogo de contactos queda en <c>ChannelUnavailable</c>, y un rol que ya no existe, en
/// <c>RoleMissing</c>, sin su nombre.
/// </summary>
public sealed class FormCatalogFiltersPolicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Sales = Guid.NewGuid();
    private static readonly Guid North = Guid.NewGuid();
    private static readonly Guid Laura = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();

    [Fact]
    public async Task Valid_member_filters_keep_their_conditions_and_the_sample_person()
    {
        var decided = await Policy().DecideAsync(FormCatalogSource.Members, Filters(
            Condition(MembersCatalogSource.Role, FormCatalogOperators.HasAny, Sales.ToString()),
            Condition(MembersCatalogSource.Company, FormCatalogOperators.HasNone, North.ToString()),
            Condition(MembersCatalogSource.Name, FormCatalogOperators.Contains, " Ríos "),
            Condition(MembersCatalogSource.Active, FormCatalogOperators.Is, "true")) with { Detail = MembersCatalogSource.Role },
            Laura, Ct);

        Assert.True(decided.IsSuccess, decided.IsFailure ? decided.Error.Code : null);
        Assert.Equal(Laura, decided.Value.SampleId);
        Assert.Equal("Ríos", decided.Value.Conditions[2].Values[0]);
        Assert.Equal(MembersCatalogSource.Role, decided.Value.Detail);
    }

    [Theory]
    [InlineData("email", FormCatalogOperators.Contains, "a")]
    [InlineData(MembersCatalogSource.Role, FormCatalogOperators.Contains, "a")]
    [InlineData(MembersCatalogSource.Role, FormCatalogOperators.HasAny, "not-an-id")]
    [InlineData(MembersCatalogSource.Active, FormCatalogOperators.Is, "yes")]
    [InlineData(MembersCatalogSource.Name, FormCatalogOperators.EqualTo, "")]
    public async Task A_column_operator_or_value_outside_the_source_marks_its_condition(string column, string op, string value)
    {
        var decided = await Policy().DecideAsync(FormCatalogSource.Members, Filters(
            Condition(MembersCatalogSource.Active, FormCatalogOperators.Is, "true"), Condition(column, op, value)), Laura, Ct);

        AssertField(decided, FormCatalogErrors.FiltersInvalidCode, "conditions.1");
    }

    [Fact]
    public async Task A_role_or_company_that_isnt_in_the_organization_marks_its_condition()
    {
        var role = await Policy().DecideAsync(FormCatalogSource.Members, Filters(
            Condition(MembersCatalogSource.Role, FormCatalogOperators.HasAny, Sales.ToString(), Guid.NewGuid().ToString())), Laura, Ct);
        AssertField(role, FormCatalogErrors.FiltersInvalidCode, "conditions.0");

        var company = await Policy().DecideAsync(FormCatalogSource.Members, Filters(
            Condition(MembersCatalogSource.Active, FormCatalogOperators.Is, "false"),
            Condition(MembersCatalogSource.Company, FormCatalogOperators.HasAny, Guid.NewGuid().ToString())), Laura, Ct);
        AssertField(company, FormCatalogErrors.FiltersInvalidCode, "conditions.1");
    }

    [Fact]
    public async Task Detail_sort_and_source_version_are_checked_against_the_source()
    {
        AssertField(await Policy().DecideAsync(FormCatalogSource.Members, Filters() with { Detail = MembersCatalogSource.Active }, null, Ct),
            FormCatalogErrors.FiltersInvalidCode, "detail");
        AssertField(await Policy().DecideAsync(FormCatalogSource.Members, Filters() with { Sort = new(MembersCatalogSource.Role, false) }, null, Ct),
            FormCatalogErrors.FiltersInvalidCode, "sort");
        AssertField(await Policy().DecideAsync(FormCatalogSource.Members, Filters() with { SourceVersion = 2 }, null, Ct),
            FormCatalogErrors.FiltersInvalidCode, "sourceVersion");
        AssertField(await Policy().DecideAsync(FormCatalogSource.Contacts, Filters(
                Condition(ContactsCatalogSource.Name, FormCatalogOperators.Contains, "Ana")), null, Ct),
            FormCatalogErrors.FiltersInvalidCode, "conditions.0");
        Assert.Equal(FormCatalogErrors.WrongSourceCode, (await Policy().DecideAsync(FormCatalogSource.Manual, Filters(), null, Ct)).Error.Code);
    }

    [Fact]
    public async Task Member_conditions_need_a_sample_person_of_the_organization_and_a_customer_must_be_in_Customers()
    {
        var conditions = Filters(Condition(MembersCatalogSource.Active, FormCatalogOperators.Is, "true"));
        AssertField(await Policy().DecideAsync(FormCatalogSource.Members, conditions, null, Ct), FormCatalogErrors.SampleRequiredCode,
            "sampleMemberId");
        AssertField(await Policy().DecideAsync(FormCatalogSource.Members, conditions, Guid.NewGuid(), Ct),
            FormCatalogErrors.SampleMemberNotFoundCode, "sampleMemberId");
        // Sin condiciones no hace falta la persona de ejemplo.
        Assert.True((await Policy().DecideAsync(FormCatalogSource.Members, Filters(), null, Ct)).IsSuccess);

        var tags = Filters(Condition(ContactsCatalogSource.Tags, FormCatalogOperators.HasAny, "VIP"));
        Assert.True((await Policy().DecideAsync(FormCatalogSource.Contacts, tags, Customer, Ct)).IsSuccess);
        AssertField(await Policy().DecideAsync(FormCatalogSource.Contacts, tags, Guid.NewGuid(), Ct),
            FormCatalogErrors.SampleContactNotFoundCode, "sampleContactId");
        AssertField(await Policy(channel: false).DecideAsync(FormCatalogSource.Contacts, tags, Customer, Ct),
            FormCatalogErrors.SampleContactNotFoundCode, "sampleContactId");
    }

    [Fact]
    public async Task Reading_without_the_channel_gives_ChannelUnavailable_and_a_missing_role_gives_RoleMissing_without_its_name()
    {
        var missing = Guid.NewGuid();
        var contacts = Summary(FormCatalogSource.Contacts, new FormCatalogFilters(ContactsCatalogSource.Version, [], true, null, null, null));
        var members = Summary(FormCatalogSource.Members, new FormCatalogFilters(MembersCatalogSource.Version,
            [new FormCatalogCondition(MembersCatalogSource.Role, FormCatalogOperators.HasAny, [Sales.ToString(), missing.ToString()])],
            true, null, null, Laura));
        var outdated = Summary(FormCatalogSource.Members, new FormCatalogFilters(0, [], true, null, null, null));

        var off = await Policy(channel: false).DecideStatesAsync([contacts, members, outdated], Ct);
        Assert.Equal(FormCatalogUnusableReason.ChannelUnavailable, off[contacts.Id].Reason);
        Assert.Equal(FormCatalogUnusableReason.RoleMissing, off[members.Id].Reason);
        Assert.Equal([new FormCatalogReference(Sales, "Ventas")], off[members.Id].Roles);
        Assert.Equal(FormCatalogUnusableReason.SourceOutdated, off[outdated.Id].Reason);

        var on = await Policy().DecideStatesAsync([contacts], Ct);
        Assert.Null(on[contacts.Id].Reason);
    }

    private static FormCatalogFiltersPolicy Policy(bool channel = true) => new(new Directory(), new Contacts(channel));

    private static FormCatalogFiltersInput Filters(params FormCatalogConditionInput[] conditions) =>
        new(MembersCatalogSource.Version, conditions, true, null, null);

    private static FormCatalogConditionInput Condition(string column, string op, params string[] values) => new(column, op, values);

    private static FormCatalogStoredRow Summary(FormCatalogSource source, FormCatalogFilters filters) =>
        new(Guid.NewGuid(), "Catálogo", null, source, FormCatalogValueType.Text, filters, true, 0, 0, DateTime.UnixEpoch, null, 1);

    private static void AssertField<T>(Result<T> result, string code, string field)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal([field], Assert.IsType<ValidationError>(result.Error).Errors.Keys);
    }

    private sealed class Directory : IMemberDirectoryReader
    {
        public Task<MemberDirectoryResponse> ListAsync(MemberDirectoryQuery query, CancellationToken ct) => throw new NotSupportedException();

        public Task<MemberDirectorySampleRow?> FindSampleAsync(Guid memberId, CancellationToken ct) =>
            Task.FromResult(memberId == Laura ? new MemberDirectorySampleRow(Laura, "Laura Ríos", ["Norte"], ["Ventas"]) : null);

        public Task<PagedResult<MemberDirectorySampleRow>> ListSamplesAsync(PagedRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<MemberDirectoryNameRow>> ListRolesAsync(PagedRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<MemberDirectoryNameRow>> ListCompaniesAsync(PagedRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MemberDirectoryNameRow>> ListRolesByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MemberDirectoryNameRow>>([.. ids.Where(id => id == Sales).Select(id => new MemberDirectoryNameRow(id, "Ventas"))]);

        public Task<IReadOnlyList<MemberDirectoryNameRow>> ListCompaniesByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MemberDirectoryNameRow>>([.. ids.Where(id => id == North).Select(id => new MemberDirectoryNameRow(id, "Norte"))]);
    }

    private sealed class Contacts(bool channel) : IStructuredContactSource
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(channel);

        public Task<StructuredContactPage> ListAsync(StructuredContactQuery query, CancellationToken ct) => throw new NotSupportedException();

        public Task<StructuredContact?> FindAsync(Guid contactId, CancellationToken ct) =>
            Task.FromResult(channel && contactId == Customer ? new StructuredContact(Customer, "Ana", ["VIP"]) : null);

        public Task<PagedResult<StructuredContact>> ListPageAsync(PagedRequest request, CancellationToken ct) => throw new NotSupportedException();

        public Task<PagedResult<StructuredContactTag>> ListTagsAsync(PagedRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
