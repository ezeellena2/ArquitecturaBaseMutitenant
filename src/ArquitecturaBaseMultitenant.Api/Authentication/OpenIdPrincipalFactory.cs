using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.Authentication;

/// <summary>Emite claims sólo del acceso elegido, con datos actuales de la cuenta.</summary>
public sealed class OpenIdPrincipalFactory(IConnectService service, IOpenIddictScopeManager scopeManager)
{
    public async Task<ClaimsPrincipal?> CreateAsync(Guid userId, Access access, Guid? requestedTenantId,
        ImmutableArray<string> scopes, CancellationToken cancellationToken, DateTime? sessionStartedAtUtc = null)
    {
        var selected = await service.GetActiveUserAsync(userId, access, requestedTenantId, cancellationToken);
        if (selected.IsFailure || !IsConsistent(selected.Value, userId, access))
        {
            return null;
        }

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name, Claims.Role);
        SetUserClaims(identity, selected.Value);
        identity.SetClaim(BrowserSessionKeys.StartedAtUtc,
            sessionStartedAtUtc is { } startedAtUtc ? BrowserSessionKeys.FormatStartedAtUtc(startedAtUtc) : null);
        identity.SetScopes(scopes);
        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(scopes, cancellationToken))
        {
            resources.Add(resource);
        }
        identity.SetResources(resources);
        identity.SetDestinations(GetDestinations);
        return new ClaimsPrincipal(identity);
    }

    public async Task<ClaimsPrincipal?> RefreshAsync(ClaimsPrincipal stored, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(stored.GetClaim(Claims.Subject), CultureInfo.InvariantCulture, out var userId) ||
            !Enum.TryParse<Access>(stored.GetClaim(TenantClaimTypes.Access), true, out var access) ||
            !Enum.IsDefined(access))
        {
            return null;
        }

        var tenantClaim = stored.GetClaim(TenantClaimTypes.TenantId);
        Guid? tenantId = null;
        if (tenantClaim is not null)
        {
            if (!Guid.TryParse(tenantClaim, CultureInfo.InvariantCulture, out var parsedTenantId))
            {
                return null;
            }
            tenantId = parsedTenantId;
        }
        if ((access == Access.Platform) != (tenantId is null))
        {
            return null;
        }

        return await CreateAsync(userId, access, tenantId, stored.GetScopes(), cancellationToken,
            BrowserSessionKeys.ReadStartedAtUtc(stored.GetClaim(BrowserSessionKeys.StartedAtUtc)));
    }

    private static bool IsConsistent(ConnectUser user, Guid userId, Access access) =>
        user.Account.Id == userId && user.Access == access &&
        (access switch
        {
            Access.Consumer => user.TenantId is not null && user.TenantKind == TenantKind.Personal,
            Access.Business => user.TenantId is not null && user.TenantKind == TenantKind.Business,
            Access.Platform => user.TenantId is null && user.TenantKind is null,
            _ => false,
        });

    private static void SetUserClaims(ClaimsIdentity identity, ConnectUser user)
    {
        identity.SetClaim(Claims.Subject, user.Account.Id.ToString("D", CultureInfo.InvariantCulture));
        identity.SetClaim(Claims.Name, user.Account.DisplayName ?? user.Account.PrimaryEmail?.Value);
        identity.SetClaim(Claims.Email, user.Account.PrimaryEmail?.Value);
        identity.SetClaim(TenantClaimTypes.Access, user.Access.ToString().ToLowerInvariant());
        identity.SetClaim(TenantClaimTypes.TenantId, user.TenantId?.ToString("D", CultureInfo.InvariantCulture));
        identity.SetClaim(TenantClaimTypes.TenantKind, user.TenantKind?.ToString().ToLowerInvariant());
    }

    private static IEnumerable<string> GetDestinations(Claim claim) => claim.Type switch
    {
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        BrowserSessionKeys.StartedAtUtc => [Destinations.AccessToken],
        Claims.Email when claim.Subject?.HasScope(Scopes.Email) == true =>
            [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name when claim.Subject?.HasScope(Scopes.Profile) == true =>
            [Destinations.AccessToken, Destinations.IdentityToken],
        TenantClaimTypes.Access or TenantClaimTypes.TenantId or TenantClaimTypes.TenantKind =>
            [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name or Claims.Email =>
            [Destinations.AccessToken],
        _ => [],
    };
}
