using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Comprueba que los tres atributos exijan su clase de acceso y que la empresa venga de la ruta. Una empresa que no es de
/// la organización activa falla como la que no existe (E9, CC2).
/// </summary>
public sealed class PermissionAuthorizationTests
{
    [Fact]
    public async Task Organization_permission_requires_business_access_and_known_grant()
    {
        var tenant = Guid.CreateVersion7();
        Assert.True(await AuthorizeAsync(PermissionPolicyProvider.OrganizationPrefix + "users.read",
            Access.Business, tenant, TenantKind.Business, organizationGrant: true));
        Assert.False(await AuthorizeAsync(PermissionPolicyProvider.OrganizationPrefix + "users.read",
            Access.Consumer, tenant, TenantKind.Personal, organizationGrant: true));
        Assert.False(await AuthorizeAsync(PermissionPolicyProvider.OrganizationPrefix + "unknown.read",
            Access.Business, tenant, TenantKind.Business));
    }

    [Fact]
    public async Task Company_permission_requires_company_route_and_grant_for_that_company()
    {
        var tenant = Guid.CreateVersion7();
        var company = Guid.CreateVersion7();
        Assert.True(await AuthorizeAsync(PermissionPolicyProvider.CompanyPrefix + "company.members.read",
            Access.Business, tenant, TenantKind.Business, companyGrant: true, companyId: company));
        Assert.False(await AuthorizeAsync(PermissionPolicyProvider.CompanyPrefix + "company.members.read",
            Access.Business, tenant, TenantKind.Business, companyGrant: true));
        Assert.False(await AuthorizeAsync(PermissionPolicyProvider.CompanyPrefix + "company.members.read",
            Access.Consumer, tenant, TenantKind.Personal, companyGrant: true, companyId: company));
    }

    // La ficha de una empresa: la abre quien ve todas las empresas y quien tiene el permiso en esa empresa (B4).
    [Fact]
    public async Task Company_permission_with_an_organization_alternative_accepts_either_grant()
    {
        var tenant = Guid.CreateVersion7();
        var company = Guid.CreateVersion7();
        var policy = new HasCompanyPermissionAttribute("company.members.read", organizationPermission: "companies.read")
            .Policy!;
        Assert.True(await AuthorizeAsync(policy, Access.Business, tenant, TenantKind.Business,
            organizationGrant: true, companyId: company));
        Assert.True(await AuthorizeAsync(policy, Access.Business, tenant, TenantKind.Business,
            companyGrant: true, companyId: company));
        Assert.False(await AuthorizeAsync(policy, Access.Business, tenant, TenantKind.Business, companyId: company));
        Assert.False(await AuthorizeAsync(policy, Access.Business, tenant, TenantKind.Business, companyGrant: true));
        Assert.False(await AuthorizeAsync(policy, Access.Consumer, tenant, TenantKind.Personal,
            organizationGrant: true, companyGrant: true, companyId: company));
    }

    [Fact]
    public async Task Platform_permission_requires_platform_access()
    {
        var tenant = Guid.CreateVersion7();
        Assert.True(await AuthorizeAsync(PermissionPolicyProvider.PlatformPrefix + "platform.tenants.read",
            Access.Platform, tenant, tenantKind: null, platformGrant: true));
        Assert.False(await AuthorizeAsync(PermissionPolicyProvider.PlatformPrefix + "platform.tenants.read",
            Access.Business, tenant, TenantKind.Business, platformGrant: true));
        Assert.False(await AuthorizeAsync(PermissionPolicyProvider.PlatformPrefix + "platform.unknown",
            Access.Platform, tenant, tenantKind: null));
    }

    // E9 (CC2): la empresa de otra organización responde 404 como la que no existe; la propia sin permiso, 403.
    [Fact]
    public async Task A_company_outside_the_organization_fails_as_not_found_and_an_own_one_as_forbidden()
    {
        var tenant = Guid.CreateVersion7();
        var company = Guid.CreateVersion7();
        var policy = PermissionPolicyProvider.CompanyPrefix + "company.members.read";

        var foreign = await HandleAsync(policy, Access.Business, tenant, TenantKind.Business, companyId: company,
            companyInOrganization: false);
        var own = await HandleAsync(policy, Access.Business, tenant, TenantKind.Business, companyId: company);

        Assert.Contains(foreign.FailureReasons, reason => reason is CompanyNotFoundReason);
        Assert.False(own.HasSucceeded);
        Assert.Empty(own.FailureReasons);
    }

    private static async Task<bool> AuthorizeAsync(string policyName, Access access, Guid? tenantId,
        TenantKind? tenantKind, bool organizationGrant = false, bool companyGrant = false,
        bool platformGrant = false, Guid? companyId = null) =>
        (await HandleAsync(policyName, access, tenantId, tenantKind, organizationGrant, companyGrant, platformGrant,
            companyId)).HasSucceeded;

    private static async Task<AuthorizationHandlerContext> HandleAsync(string policyName, Access access, Guid? tenantId,
        TenantKind? tenantKind, bool organizationGrant = false, bool companyGrant = false,
        bool platformGrant = false, Guid? companyId = null, bool companyInOrganization = true)
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync(policyName);
        Assert.NotNull(policy);
        var current = new TestCurrentUser(access);
        var tenant = new TestTenantContext(tenantId, tenantKind);
        var permissions = new TestPermissions(organizationGrant, companyGrant);
        var platformPermissions = new TestPlatformPermissions(platformGrant);
        var handler = new PermissionAuthorizationHandler(current, tenant, permissions, platformPermissions,
            new TestCompanies(companyInOrganization));
        var request = new DefaultHttpContext();
        if (companyId is { } id) request.Request.RouteValues["companyId"] = id.ToString("D");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,
            Guid.CreateVersion7().ToString("D"))], "test"));
        var context = new AuthorizationHandlerContext(policy!.Requirements
            .Where(requirement => requirement is PermissionRequirement), principal, request);
        await handler.HandleAsync(context);
        return context;
    }

    private sealed class TestCurrentUser(Access access) : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.CreateVersion7();
        public Access? Access { get; } = access;
    }

    private sealed class TestTenantContext(Guid? tenantId, TenantKind? tenantKind) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public TenantKind? TenantKind { get; } = tenantKind;
        public Guid RequiredTenantId => TenantId ?? throw new InvalidOperationException();
    }

    private sealed class TestPermissions(bool organizationGrant, bool companyGrant) : IPermissionService
    {
        public Task<IReadOnlyList<string>> GetAssignedOrganizationPermissionsAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(new Dictionary<Guid, IReadOnlyList<string>>());
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct) => Task.FromResult(organizationGrant);
        public Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission, CancellationToken ct) => Task.FromResult(companyGrant);
    }

    private sealed class TestCompanies(bool inOrganization) : ICompanyService
    {
        public Task<Result<CompanyResponse>> GetAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(inOrganization
                ? Result.Success(new CompanyResponse(companyId, "Propia", null, "UTC", CompanyStatus.Active, 1, 1, DateTime.UnixEpoch))
                : Result.Failure<CompanyResponse>(CompanyErrors.NotFound));
        public Task<Result<PagedResult<CompanyResponse>>> ListAsync(ListCompaniesRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<CompanyFilterCountsResponse>> GetFilterCountsAsync(ListCompaniesRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<Guid>> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<VersionResponse>> UpdateAsync(UpdateCompanyRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Result<VersionResponse>> ChangeStatusAsync(ChangeCompanyStatusRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestPlatformPermissions(bool grant) : IPlatformPermissionService
    {
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct) => Task.FromResult(grant);
    }
}
