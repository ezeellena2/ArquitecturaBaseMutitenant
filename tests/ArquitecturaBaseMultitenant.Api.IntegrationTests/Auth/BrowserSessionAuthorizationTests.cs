using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class BrowserSessionAuthorizationTests(ApiFactory factory)
{
    [Fact]
    public async Task Revoking_one_browser_session_keeps_the_same_users_other_session_valid()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        await factory.Services.SeedDatabaseAsync(ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var store = services.GetRequiredService<IConnectAuthorizationStore>();
        var revoker = services.GetRequiredService<ITokenRevoker>();
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var userId = Guid.NewGuid();
        string? firstId = null;
        string? secondId = null;
        var created = await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            firstId = await store.CreateAsync(userId, Access.Consumer, null,
                "first-browser-session", "web", [Scopes.OpenId], inner);
            secondId = await store.CreateAsync(userId, Access.Consumer, null,
                "second-browser-session", "web", [Scopes.OpenId], inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);
        Assert.True(created.IsSuccess);

        var revoked = await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            await revoker.RevokeSessionAsync(userId, "first-browser-session", inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);
        Assert.True(revoked.IsSuccess);

        var first = await authorizations.FindByIdAsync(firstId!, ct);
        var second = await authorizations.FindByIdAsync(secondId!, ct);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(Statuses.Revoked, await authorizations.GetStatusAsync(first, ct));
        Assert.Equal(Statuses.Valid, await authorizations.GetStatusAsync(second, ct));
    }
}
