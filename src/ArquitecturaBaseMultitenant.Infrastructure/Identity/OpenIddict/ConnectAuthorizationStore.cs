using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.OpenIddict;

internal sealed class ConnectAuthorizationStore(
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    ApplicationDbContext context) : IConnectAuthorizationStore
{
    internal const string SessionIdProperty = "session_id";

    public async Task<string> CreateAsync(Guid userId, Access access, Guid? tenantId,
        string sessionId, string clientId, IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var application = await applications.FindByClientIdAsync(clientId, cancellationToken)
            ?? throw new InvalidOperationException("The OpenID Connect client does not exist.");
        var applicationId = await applications.GetIdAsync(application, cancellationToken)
            ?? throw new InvalidOperationException("The OpenID Connect client has no identifier.");
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = applicationId,
            Subject = userId.ToString("D"),
            Status = Statuses.Valid,
            Type = AuthorizationTypes.AdHoc,
        };
        descriptor.Scopes.UnionWith(scopes);
        descriptor.Properties[SessionIdProperty] = JsonSerializer.SerializeToElement(sessionId);

        var authorization = await authorizations.CreateAsync(descriptor, cancellationToken);
        return await authorizations.GetIdAsync(authorization, cancellationToken)
            ?? throw new InvalidOperationException("The OpenID Connect authorization has no identifier.");
    }
}
