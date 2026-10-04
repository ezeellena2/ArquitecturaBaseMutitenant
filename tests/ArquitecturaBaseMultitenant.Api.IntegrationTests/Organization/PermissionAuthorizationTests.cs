using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>Comprueba que los tres atributos exijan su clase de acceso y que la empresa venga de la ruta.</summary>
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

    private static async Task<bool> AuthorizeAsync(string policyName, Access access, Guid? tenantId,
        TenantKind? tenantKind, bool organizationGrant = false, bool companyGrant = false,
        bool platformGrant = false, Guid? companyId = null)
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync(policyName);
        Assert.NotNull(policy);
        var current = new TestCurrentUser(access);
        var tenant = new TestTenantContext(tenantId, tenantKind);
        var permissions = new TestPermissions(organizationGrant, companyGrant);
        var platformPermissions = new TestPlatformPermissions(platformGrant);
        var handler = new PermissionAuthorizationHandler(current, tenant, permissions, platformPermissions);
        var request = new DefaultHttpContext();
        if (companyId is { } id) request.Request.RouteValues["companyId"] = id.ToString("D");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,
            Guid.CreateVersion7().ToString("D"))], "test"));
        var context = new AuthorizationHandlerContext(policy!.Requirements
            .Where(requirement => requirement is PermissionRequirement), principal, request);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
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
        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> GetCompanyPermissionsAsync(Guid userId, Guid companyId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetPermissionsByCompanyAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(new Dictionary<Guid, IReadOnlyList<string>>());
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct) => Task.FromResult(organizationGrant);
        public Task<bool> HasCompanyPermissionAsync(Guid userId, Guid companyId, string permission, CancellationToken ct) => Task.FromResult(companyGrant);
        public Task InvalidateRoleAsync(Guid roleId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TestPlatformPermissions(bool grant) : IPlatformPermissionService
    {
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct) => Task.FromResult(grant);
    }
}
