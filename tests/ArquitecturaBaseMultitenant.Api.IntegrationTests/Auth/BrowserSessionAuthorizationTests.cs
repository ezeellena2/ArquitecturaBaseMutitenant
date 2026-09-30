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

    [Fact]
    public async Task Revoking_business_access_keeps_consumer_authorization_valid()
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
        string? consumerId = null;
        string? businessId = null;

        await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            consumerId = await store.CreateAsync(userId, Access.Consumer, null,
                "same-browser", "web", [Scopes.OpenId], inner);
            businessId = await store.CreateAsync(userId, Access.Business, Guid.NewGuid(),
                "same-browser", "web", [Scopes.OpenId], inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);

        await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            await revoker.RevokeAccessAsync(userId, Access.Business, inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);

        var consumer = await authorizations.FindByIdAsync(consumerId!, ct);
        var business = await authorizations.FindByIdAsync(businessId!, ct);
        Assert.NotNull(consumer);
        Assert.NotNull(business);
        Assert.Equal(Statuses.Valid, await authorizations.GetStatusAsync(consumer, ct));
        Assert.Equal(Statuses.Revoked, await authorizations.GetStatusAsync(business, ct));
    }

    [Fact]
    public async Task Revoking_tenant_keeps_other_tenant_authorization_valid()
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
        var tenantId = Guid.NewGuid();
        string? targetId = null;
        string? otherId = null;

        await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            targetId = await store.CreateAsync(Guid.NewGuid(), Access.Business, tenantId,
                "target-browser", "web", [Scopes.OpenId], inner);
            otherId = await store.CreateAsync(Guid.NewGuid(), Access.Business, Guid.NewGuid(),
                "other-browser", "web", [Scopes.OpenId], inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);

        await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            await revoker.RevokeTenantAsync(tenantId, inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);

        var target = await authorizations.FindByIdAsync(targetId!, ct);
        var other = await authorizations.FindByIdAsync(otherId!, ct);
        Assert.NotNull(target);
        Assert.NotNull(other);
        Assert.Equal(Statuses.Revoked, await authorizations.GetStatusAsync(target, ct));
        Assert.Equal(Statuses.Valid, await authorizations.GetStatusAsync(other, ct));
    }
}
