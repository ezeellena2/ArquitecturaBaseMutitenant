using System.Reflection;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.Authentication;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

public sealed class PrincipalFactoryTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PersonalId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BusinessId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Theory]
    [InlineData(Access.Consumer, TenantKind.Personal)]
    [InlineData(Access.Business, TenantKind.Business)]
    public async Task Access_token_contains_only_identity_and_the_selected_access(Access access, TenantKind kind)
    {
        var tenantId = access == Access.Consumer ? PersonalId : BusinessId;
        var (factory, _) = CreateFactory(new ConnectUser(Account(), access, tenantId, kind));

        var principal = await factory.CreateAsync(UserId, access, tenantId,
            [Scopes.OpenId, Scopes.Profile, Scopes.Email, "api"], TestContext.Current.CancellationToken);

        Assert.NotNull(principal);
        Assert.Equal(UserId.ToString("D"), principal.GetClaim(Claims.Subject));
        Assert.Equal("Ana", principal.GetClaim(Claims.Name));
        Assert.Equal("ana@example.test", principal.GetClaim(Claims.Email));
        Assert.Equal(access.ToString().ToLowerInvariant(), principal.GetClaim(TenantClaimTypes.Access));
        Assert.Equal(tenantId.ToString("D"), principal.GetClaim(TenantClaimTypes.TenantId));
        Assert.Equal(kind.ToString().ToLowerInvariant(), principal.GetClaim(TenantClaimTypes.TenantKind));
        Assert.Null(principal.GetClaim(Claims.Role));
        Assert.DoesNotContain(principal.Claims, claim => claim.Type.Contains("permission", StringComparison.OrdinalIgnoreCase));
        Assert.Equal([Destinations.AccessToken, Destinations.IdentityToken],
            principal.FindFirst(Claims.Subject)!.GetDestinations());
        Assert.Equal([Destinations.AccessToken],
            principal.FindFirst(TenantClaimTypes.Access)!.GetDestinations());
        Assert.Equal([Destinations.AccessToken],
            principal.FindFirst(TenantClaimTypes.TenantId)!.GetDestinations());
        Assert.Equal([Destinations.AccessToken],
            principal.FindFirst(TenantClaimTypes.TenantKind)!.GetDestinations());
        Assert.Equal([Destinations.AccessToken, Destinations.IdentityToken],
            principal.FindFirst(Claims.Name)!.GetDestinations());
        Assert.Equal([Destinations.AccessToken, Destinations.IdentityToken],
            principal.FindFirst(Claims.Email)!.GetDestinations());
    }

    [Fact]
    public async Task Platform_access_has_no_tenant_and_refresh_revalidates_the_access()
    {
        var (factory, connect) = CreateFactory(new ConnectUser(Account(), Access.Platform, null, null));
        var principal = await factory.CreateAsync(UserId, Access.Platform, null,
            [Scopes.OpenId], TestContext.Current.CancellationToken);

        Assert.NotNull(principal);
        Assert.Equal("platform", principal.GetClaim(TenantClaimTypes.Access));
        Assert.Null(principal.GetClaim(TenantClaimTypes.TenantId));
        Assert.Null(principal.GetClaim(TenantClaimTypes.TenantKind));
        Assert.Equal([Destinations.AccessToken], principal.FindFirst(Claims.Name)!.GetDestinations());
        Assert.Equal([Destinations.AccessToken], principal.FindFirst(Claims.Email)!.GetDestinations());

        connect.Response = Result.Failure<ConnectUser>(UserErrors.NotFound);
        var refreshed = await factory.RefreshAsync(principal, TestContext.Current.CancellationToken);

        Assert.Null(refreshed);
        Assert.Equal(Access.Platform, connect.LastAccess);
        Assert.Null(connect.LastTenantId);
    }

    [Fact]
    public async Task Refresh_drops_stale_email_and_preserves_the_selected_business_tenant()
    {
        var (factory, connect) = CreateFactory(new ConnectUser(Account(), Access.Business, BusinessId, TenantKind.Business));
        var principal = await factory.CreateAsync(UserId, Access.Business, BusinessId,
            [Scopes.OpenId, Scopes.Email], TestContext.Current.CancellationToken);
        Assert.NotNull(principal);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(Claims.Role, "stale-role"));
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("permissions", "stale-permission"));

        connect.Response = Result.Success(new ConnectUser(Account(email: null, name: "Ana nueva"),
            Access.Business, BusinessId, TenantKind.Business));
        var refreshed = await factory.RefreshAsync(principal, TestContext.Current.CancellationToken);

        Assert.NotNull(refreshed);
        Assert.Equal("Ana nueva", refreshed.GetClaim(Claims.Name));
        Assert.Null(refreshed.GetClaim(Claims.Email));
        Assert.Equal(BusinessId.ToString("D"), refreshed.GetClaim(TenantClaimTypes.TenantId));
        Assert.Null(refreshed.GetClaim(Claims.Role));
        Assert.Null(refreshed.GetClaim("permissions"));
        Assert.Equal(Access.Business, connect.LastAccess);
        Assert.Equal(BusinessId, connect.LastTenantId);
    }

    private static UserAccountRow Account(string? email = "ana@example.test", string? name = "Ana") =>
        new(UserId, name, "es-AR", "America/Argentina/Buenos_Aires", UserStatus.Active, false, email, null);

    private static (OpenIdPrincipalFactory Factory, ConnectProxy Connect) CreateFactory(ConnectUser user)
    {
        var service = DispatchProxy.Create<IConnectService, ConnectProxy>();
        var proxy = (ConnectProxy)(object)service;
        proxy.Response = Result.Success(user);
        var scopes = DispatchProxy.Create<IOpenIddictScopeManager, ScopeProxy>();
        return (new OpenIdPrincipalFactory(service, scopes), proxy);
    }

    public class ConnectProxy : DispatchProxy
    {
        public Result<ConnectUser> Response { get; set; } = Result.Failure<ConnectUser>(UserErrors.NotFound);
        public Access? LastAccess { get; private set; }
        public Guid? LastTenantId { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != "GetActiveUserAsync" || args is null)
            {
                throw new NotSupportedException(targetMethod?.Name);
            }

            LastAccess = (Access)args[1]!;
            LastTenantId = (Guid?)args[2];
            return Task.FromResult(Response);
        }
    }

    public class ScopeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "ListResourcesAsync"
                ? EmptyResources()
                : throw new NotSupportedException(targetMethod?.Name);

        private static async IAsyncEnumerable<string> EmptyResources()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
