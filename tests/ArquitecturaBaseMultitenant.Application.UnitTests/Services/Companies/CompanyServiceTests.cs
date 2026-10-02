using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Services.Companies;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Companies;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Companies;

public sealed class CompanyServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_maps_company_rows_and_preserves_page_metadata()
    {
        var id = Guid.CreateVersion7();
        var reader = new FakeCompanyReader
        {
            Page = new PagedResult<CompanyRow>(
                [new(id, "La empresa", "AR", "AR-CUIT", "20123456786", "America/Argentina/Buenos_Aires",
                    CompanyStatus.Active, 3, 8, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc))],
                2, 10, 14),
        };
        using var fixture = new ServiceFixture<CompanyService>(new ListCompaniesRequestValidator());
        var service = Create(reader, fixture);

        var result = await service.ListAsync(new ListCompaniesRequest { Page = 2, Search = "empresa" }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((2, 10, 14, 2), (result.Value.Page, result.Value.PageSize,
            result.Value.TotalCount, result.Value.TotalPages));
        var company = Assert.Single(result.Value.Items);
        Assert.Equal(id, company.Id);
        Assert.Equal(new CompanyTaxIdResponse("AR", "AR-CUIT", "20123456786"), company.TaxId);
        Assert.Equal("empresa", reader.ListRequest?.Search);
    }

    [Fact]
    public async Task Filter_counts_validate_the_search_request_and_forward_it()
    {
        var reader = new FakeCompanyReader { Counts = new(7, 5, 2) };
        using var fixture = new ServiceFixture<CompanyService>(new ListCompaniesRequestValidator());
        var service = Create(reader, fixture);

        var result = await service.GetFilterCountsAsync(
            new ListCompaniesRequest { Search = "Norte", Status = CompanyStatus.Inactive }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CompanyFilterCountsResponse(7, 5, 2), result.Value);
        Assert.Equal("Norte", reader.CountRequest?.Search);
        Assert.Equal(CompanyStatus.Inactive, reader.CountRequest?.Status);
    }

    [Fact]
    public async Task A_missing_company_is_not_found()
    {
        using var fixture = new ServiceFixture<CompanyService>();
        var service = Create(new FakeCompanyReader(), fixture);

        var result = await service.GetAsync(Guid.CreateVersion7(), Ct);

        Assert.Equal(CompanyErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Invalid_create_does_not_open_a_transaction()
    {
        using var fixture = new ServiceFixture<CompanyService>(new CreateCompanyRequestValidator());
        var unitOfWork = new FakeUnitOfWork();
        var service = Create(new FakeCompanyReader(), fixture, unitOfWork);

        var result = await service.CreateAsync(new CreateCompanyRequest(null, null, null, null), Ct);

        Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(0, unitOfWork.Transactions);
    }

    [Fact]
    public async Task Create_assigns_the_creator_as_active_company_admin()
    {
        var userId = Guid.CreateVersion7();
        var member = Member.Invite(userId);
        Assert.True(member.Activate(new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
        var companyRepository = new FakeCompanyRepository();
        var companyMembershipRepository = new FakeCompanyMembershipRepository();
        var assignmentRepository = new FakeRoleAssignmentRepository();
        var companyAdminRole = SystemRoles.CreateCompanyAdmin();
        var unitOfWork = new FakeUnitOfWork();
        using var fixture = new ServiceFixture<CompanyService>(new CreateCompanyRequestValidator());
        var service = new CompanyService(new FakeCompanyReader(), companyRepository, companyMembershipRepository,
            new FakeMemberRepository { Member = member }, new FakeRoleRepository { Role = companyAdminRole }, assignmentRepository,
            new FakeTaxIdTypeCatalog(), new FakeTimeZoneCatalog(), new IdentifiedCurrentUser(userId),
            fixture.Validator, unitOfWork, fixture.TimeProvider, fixture.Logger);

        var result = await service.CreateAsync(new CreateCompanyRequest("Sucursal Centro", null, null,
            "America/Argentina/Buenos_Aires"), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(result.Value, companyRepository.Added?.Id);
        Assert.Equal(result.Value, companyMembershipRepository.Added?.CompanyId);
        Assert.Equal(member.Id, companyMembershipRepository.Added?.MemberId);
        Assert.Equal(CompanyMembershipStatus.Active, companyMembershipRepository.Added?.Status);
        Assert.Equal(result.Value, assignmentRepository.Added?.CompanyId);
        Assert.Equal(companyAdminRole.Id, assignmentRepository.Added?.RoleId);
    }

    [Fact]
    public async Task Changing_status_uses_the_supplied_version()
    {
        var company = Company.Create("Empresa", null, "America/Argentina/Buenos_Aires").Value;
        var repository = new FakeCompanyRepository { Retrieved = company };
        var unitOfWork = new FakeUnitOfWork();
        using var fixture = new ServiceFixture<CompanyService>(new ChangeCompanyStatusRequestValidator());
        var service = Create(new FakeCompanyReader(), fixture, unitOfWork, repository);

        var result = await service.ChangeStatusAsync(new ChangeCompanyStatusRequest(company.Id,
            CompanyStatus.Inactive, 12), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((uint)12, repository.ExpectedVersion);
        Assert.Equal(CompanyStatus.Inactive, company.Status);
        Assert.True(repository.Updated);
        Assert.Equal(1, unitOfWork.Transactions);
    }

    [Fact]
    public async Task A_tax_id_unique_race_returns_a_conflict_instead_of_a_server_error()
    {
        var userId = Guid.CreateVersion7();
        var member = Member.Invite(userId);
        Assert.True(member.Activate(new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
        var unitOfWork = new FakeUnitOfWork
        {
            CommitFailure = new UniqueConstraintViolationException
            {
                ConstraintName = "IX_Companies_TenantId_TaxCountry_TaxType_TaxNumber",
            },
        };
        using var fixture = new ServiceFixture<CompanyService>(new CreateCompanyRequestValidator());
        var service = new CompanyService(new FakeCompanyReader(), new FakeCompanyRepository(),
            new FakeCompanyMembershipRepository(), new FakeMemberRepository { Member = member },
            new FakeRoleRepository(), new FakeRoleAssignmentRepository(), new FakeTaxIdTypeCatalog(),
            new FakeTimeZoneCatalog(), new IdentifiedCurrentUser(userId), fixture.Validator, unitOfWork,
            fixture.TimeProvider, fixture.Logger);

        var result = await service.CreateAsync(new CreateCompanyRequest("Sucursal", null, null,
            "America/Argentina/Buenos_Aires"), Ct);

        Assert.Equal(CompanyErrors.TaxIdAlreadyUsed, result.Error);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    private static CompanyService Create(FakeCompanyReader reader, ServiceFixture<CompanyService> fixture,
        FakeUnitOfWork? unitOfWork = null, FakeCompanyRepository? companyRepository = null) =>
        new(reader, companyRepository ?? new FakeCompanyRepository(), new FakeCompanyMembershipRepository(),
            new FakeMemberRepository(), new FakeRoleRepository(), new FakeRoleAssignmentRepository(),
            new FakeTaxIdTypeCatalog(), new FakeTimeZoneCatalog(), new FakeCurrentUser(), fixture.Validator,
            unitOfWork ?? new FakeUnitOfWork(), fixture.TimeProvider, fixture.Logger);

    internal sealed class FakeCompanyReader : ICompanyReader
    {
        public PagedResult<CompanyRow> Page { get; init; } = new([], 1, 10, 0);
        public CompanyRow? Row { get; init; }
        public CompanyFilterCountsResponse Counts { get; init; } = new(0, 0, 0);
        public bool TaxIdExists { get; init; }
        public ListCompaniesRequest? ListRequest { get; private set; }
        public ListCompaniesRequest? CountRequest { get; private set; }
        public Task<PagedResult<CompanyRow>> ListAsync(ListCompaniesRequest request, CancellationToken cancellationToken)
        {
            ListRequest = request;
            return Task.FromResult(Page);
        }
        public Task<CompanyRow?> FindByIdAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Row);
        public Task<CompanyFilterCountsResponse> GetFilterCountsAsync(ListCompaniesRequest request,
            CancellationToken cancellationToken)
        {
            CountRequest = request;
            return Task.FromResult(Counts);
        }
        public Task<bool> ExistsTaxIdAsync(string country, string type, string number, Guid? excludedCompanyId,
            CancellationToken cancellationToken) => Task.FromResult(TaxIdExists);
    }

    internal sealed class FakeCompanyRepository : ICompanyRepository
    {
        public Company? Added { get; private set; }
        public Company? Retrieved { get; init; }
        public uint? ExpectedVersion { get; private set; }
        public bool Updated { get; private set; }
        public Task<Company?> GetByIdAsync(Guid companyId, uint expectedVersion, CancellationToken cancellationToken)
        {
            ExpectedVersion = expectedVersion;
            return Task.FromResult(Retrieved is { } company && company.Id == companyId ? company : null);
        }
        public void Add(Company company) => Added = company;
        public void MarkUpdated(Company company) => Updated = true;
        public void Remove(Company company) { }
    }

    internal sealed class FakeCompanyMembershipRepository : ICompanyMembershipRepository
    {
        public CompanyMembership? Added { get; private set; }
        public Task<CompanyMembership?> GetByIdAsync(Guid companyId, Guid memberId, uint expectedVersion,
            CancellationToken cancellationToken) => Task.FromResult<CompanyMembership?>(null);
        public Task<CompanyMembership?> FindAsync(Guid companyId, Guid memberId, CancellationToken cancellationToken) =>
            Task.FromResult<CompanyMembership?>(null);
        public void Add(CompanyMembership membership) => Added = membership;
        public void MarkUpdated(CompanyMembership membership) { }
        public void Remove(CompanyMembership membership) { }
    }

    internal sealed class FakeMemberRepository : IMemberRepository
    {
        public Member? Member { get; init; }
        public Task LockUserAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Member?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Member);
        public Task<Member?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Member);
        public void Add(Member member) { }
    }

    internal sealed class FakeRoleRepository : IRoleRepository
    {
        public Role? Role { get; init; } = SystemRoles.CreateCompanyAdmin();
        public Task<Role?> GetByIdAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Role);
        public void Add(Role role) { }
        public void MarkUpdated(Role role) { }
        public void Remove(Role role) { }
    }

    internal sealed class FakeRoleAssignmentRepository : IRoleAssignmentRepository
    {
        public RoleAssignment? Added { get; private set; }
        public Task<IReadOnlyList<RoleAssignment>> ListForMemberAsync(Guid memberId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RoleAssignment>>([]);
        public void Add(RoleAssignment assignment) => Added = assignment;
        public void Remove(RoleAssignment assignment) { }
    }

    internal sealed class FakeTaxIdTypeCatalog : ITaxIdTypeCatalog
    {
        public Task<IReadOnlyList<TaxIdTypeCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TaxIdTypeCatalogEntry>>([]);
        public Task<TaxIdTypeCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult<TaxIdTypeCatalogEntry?>(null);
    }

    internal sealed class FakeTimeZoneCatalog : ITimeZoneCatalog
    {
        public Task<IReadOnlyList<TimeZoneCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TimeZoneCatalogEntry>>([]);
        public Task<TimeZoneCatalogEntry?> FindAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<TimeZoneCatalogEntry?>(new(id, [], true, 1, []));
    }

    internal sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public ArquitecturaBaseMultitenant.Domain.Users.Access? Access => null;
    }

    private sealed class IdentifiedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public ArquitecturaBaseMultitenant.Domain.Users.Access? Access =>
            ArquitecturaBaseMultitenant.Domain.Users.Access.Business;
    }
}
