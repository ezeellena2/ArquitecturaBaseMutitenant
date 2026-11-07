using System.Data.Common;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba el aislamiento de revocaciones entre sesiones, accesos y organizaciones. Revocar una
/// autorización no debe invalidar sesiones ajenas a su alcance.
/// </summary>
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
    public async Task Revoking_tenant_revokes_only_its_authorizations_through_the_tenant_index()
    {
        var ct = TestContext.Current.CancellationToken;
        var lookups = new TenantLookupCapture();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(lookups))));
        using var client = host.CreateClient();
        await host.Services.SeedDatabaseAsync(ct);
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var store = services.GetRequiredService<IConnectAuthorizationStore>();
        var revoker = services.GetRequiredService<ITokenRevoker>();
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        string[] targetIds = [];
        string? otherId = null;
        string? consumerId = null;

        await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            targetIds =
            [
                await store.CreateAsync(userId, Access.Business, tenantId, "target-browser", "web", [Scopes.OpenId], inner),
                await store.CreateAsync(Guid.NewGuid(), Access.Business, tenantId, "colleague-browser", "web", [Scopes.OpenId], inner),
            ];
            otherId = await store.CreateAsync(Guid.NewGuid(), Access.Business, Guid.NewGuid(),
                "other-browser", "web", [Scopes.OpenId], inner);
            consumerId = await store.CreateAsync(userId, Access.Consumer, null, "target-browser", "web", [Scopes.OpenId], inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);

        await unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            await revoker.RevokeTenantAsync(tenantId, inner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);

        foreach (var targetId in targetIds)
            Assert.Equal(Statuses.Revoked, await StatusAsync(targetId));
        Assert.Equal(Statuses.Valid, await StatusAsync(otherId!));
        Assert.Equal(Statuses.Valid, await StatusAsync(consumerId!));

        // La búsqueda usa el índice por organización: con el recorrido secuencial apagado, el plan lo nombra.
        var lookup = Assert.Single(lookups.Commands);
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);
        await using (var noSequentialScan = new NpgsqlCommand("SET enable_seqscan = off", connection))
            await noSequentialScan.ExecuteNonQueryAsync(ct);
        await using var explain = new NpgsqlCommand("EXPLAIN " + lookup.Sql, connection);
        foreach (var (name, value) in lookup.Parameters) explain.Parameters.AddWithValue(name, value);
        await using var plan = await explain.ExecuteReaderAsync(ct);
        var lines = new List<string>();
        while (await plan.ReadAsync(ct)) lines.Add(plan.GetString(0));
        Assert.Contains(lines, line => line.Contains("IX_OpenIddictAuthorizations_TenantId", StringComparison.Ordinal));

        async Task<string?> StatusAsync(string id) =>
            await authorizations.GetStatusAsync(await authorizations.FindByIdAsync(id, ct) ?? throw new InvalidOperationException(id), ct);
    }

    /// <summary>Guarda las lecturas de autorizaciones por organización para comprobar su plan.</summary>
    private sealed class TenantLookupCapture : DbCommandInterceptor
    {
        public List<(string Sql, (string Name, object Value)[] Parameters)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("'tenant_id'", StringComparison.Ordinal))
                Commands.Add((command.CommandText, [.. command.Parameters.Cast<DbParameter>()
                    .Select(parameter => (parameter.ParameterName, parameter.Value ?? DBNull.Value))]));
            return ValueTask.FromResult(result);
        }
    }
}
