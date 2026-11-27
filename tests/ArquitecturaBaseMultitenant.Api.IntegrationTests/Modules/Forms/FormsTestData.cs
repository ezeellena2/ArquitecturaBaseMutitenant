using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>Lo que arman las pruebas de las rutas de Formularios: el módulo prendido y una organización con su dueño.</summary>
internal static class FormsTestData
{
    internal const string Catalogs = "/api/organization/forms/catalogs";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static HttpClient Client(ApiFactory factory) =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>Prende <c>forms</c> en la organización, como lo deja Plataforma, con su evento de seguridad.</summary>
    internal static async Task EnableAsync(ApiFactory factory, Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(
                TenantFeature.Create(tenantId, "forms", true, TenantFeatureSource.Manual, null).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System,
                Guid.Empty, tenantId, "Forms fixture grant", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
    }

    /// <summary>Una organización nueva con <c>forms</c> prendido y el cliente de su dueño, que entra como empresa.</summary>
    internal static async Task<(HttpClient Client, Guid TenantId, Guid OwnerId)> BusinessAsync(ApiFactory factory, string name)
    {
        var client = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, name + " " + Guid.NewGuid().ToString("N")[..8], Ct);
        await EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        return (client, tenantId, owner.UserId);
    }

    /// <summary>Un catálogo a mano con sus opciones, guardado por el servicio con la organización en el alcance.</summary>
    internal static async Task<FormCatalogResponse> CreateManualAsync(ApiFactory factory, Guid tenantId, string name,
        params (string Code, string Text)[] items)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var created = await services.GetRequiredService<IFormCatalogService>().CreateAsync(new CreateFormCatalogRequest(name, null,
            FormCatalogSource.Manual, null,
            [.. items.Select((item, index) => new FormCatalogItemInput(null, $"n{index}", item.Code, item.Text, null, null, true))],
            null, null, null), Ct);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
        return created.Value.Catalog;
    }

    /// <summary>Un miembro de la organización que entra como empresa, sin roles o con un rol que da solo ese permiso.</summary>
    internal static async Task<HttpClient> MemberAsync(ApiFactory factory, Guid tenantId, string? permission)
    {
        var client = Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, account.UserId, Ct);
        if (permission is not null)
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var role = Role.Create("Solo " + permission, null, RoleScope.Organization, null, [permission]).Value;
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.Add(role);
                db.Add(RoleAssignment.Assign(memberId, role.Id, role.Scope, null, null).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        return client;
    }

    /// <summary>
    /// Espera a que <paramref name="count"/> transacciones esperen el advisory lock de esa clave (<c>hashtextextended</c>, como
    /// <c>AcquireAdvisoryLocksAsync</c> y la barrera de archivos): así una prueba sabe que dos pedidos llegaron al lock a la vez.
    /// </summary>
    internal static async Task WaitForLockWaitersAsync(ApiFactory factory, string key, int count)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            await using var command = new NpgsqlCommand("""
                SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted
                AND ((classid::bigint << 32) | objid::bigint) = hashtextextended(@key, 0)
                """, connection);
            command.Parameters.AddWithValue("key", key);
            if ((long)(await command.ExecuteScalarAsync(deadline.Token))! >= count) return;
            await Task.Delay(50, deadline.Token);
        }
    }

    /// <summary>Toma el advisory lock de esa clave en una transacción propia, que lo tiene hasta que se confirma o se descarta.</summary>
    internal static async Task<(NpgsqlConnection Connection, NpgsqlTransaction Transaction)> HoldLockAsync(ApiFactory factory, string key)
    {
        var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        var transaction = await connection.BeginTransactionAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, transaction);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync(Ct);
        return (connection, transaction);
    }
}
