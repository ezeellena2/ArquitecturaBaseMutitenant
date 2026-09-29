using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using OpenIddict.Abstractions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;

/// <summary>Invalida de inmediato autorizaciones y tokens del acceso indicado.</summary>
internal sealed class TokenRevoker(
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens,
    ApplicationDbContext context) : ITokenRevoker
{
    internal const string AccessProperty = "access";
    internal const string TenantIdProperty = "tenant_id";

    public async Task RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var authorization = await authorizations.FindByIdAsync(authorizationId, cancellationToken);
        if (authorization is null)
        {
            return;
        }

        await tokens.RevokeByAuthorizationIdAsync(authorizationId, cancellationToken);
        await authorizations.TryRevokeAsync(authorization, cancellationToken);
    }

    public async Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var subject = userId.ToString("D");
        await authorizations.RevokeBySubjectAsync(subject, cancellationToken);
        await tokens.RevokeBySubjectAsync(subject, cancellationToken);
    }

    public Task RevokeAccessAsync(Guid userId, Access access, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var value = access switch
        {
            Access.Consumer => "consumer",
            Access.Business => "business",
            Access.Platform => "platform",
            _ => throw new ArgumentOutOfRangeException(nameof(access)),
        };
        return RevokeMatchingAsync(authorizations.FindBySubjectAsync(userId.ToString("D"), cancellationToken),
            properties => HasValue(properties, AccessProperty, value), cancellationToken);
    }

    public Task RevokeTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return RevokeMatchingAsync(authorizations.ListAsync(cancellationToken: cancellationToken),
            properties => HasValue(properties, TenantIdProperty, tenantId.ToString("D")), cancellationToken);
    }

    private async Task RevokeMatchingAsync(
        IAsyncEnumerable<object> source,
        Func<IReadOnlyDictionary<string, JsonElement>, bool> matches,
        CancellationToken cancellationToken)
    {
        await foreach (var authorization in source.WithCancellation(cancellationToken))
        {
            if (!matches(await authorizations.GetPropertiesAsync(authorization, cancellationToken)))
            {
                continue;
            }

            var id = await authorizations.GetIdAsync(authorization, cancellationToken);
            if (id is not null)
            {
                await tokens.RevokeByAuthorizationIdAsync(id, cancellationToken);
            }
            await authorizations.TryRevokeAsync(authorization, cancellationToken);
        }
    }

    private static bool HasValue(IReadOnlyDictionary<string, JsonElement> properties, string key, string value) =>
        properties.TryGetValue(key, out var element) &&
        element.ValueKind == JsonValueKind.String &&
        string.Equals(element.GetString(), value, StringComparison.OrdinalIgnoreCase);
}
