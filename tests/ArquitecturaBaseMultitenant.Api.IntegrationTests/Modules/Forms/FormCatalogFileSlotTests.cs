using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// La barrera en las dos rutas multipart de los catálogos (C11), con <c>FormCatalogFileSlotAttribute</c>: con la de la persona
/// tomada, la vista previa y aplicar responden 409 Busy antes de la idempotencia, que lee el formulario para su huella, así que no
/// se lee el archivo; con su organización sin lugar, 429 TooManyAtOnce con <c>Retry-After</c>; y el pedido que pasa la suelta al
/// terminar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogFileSlotTests(ApiFactory factory)
{
    private const string Catalogs = "/api/organization/forms/catalogs/";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_the_person_s_barrier_taken_both_routes_answer_busy_without_reading_the_file_and_a_free_one_is_given_back()
    {
        var (client, tenantId, userId) = await BusinessAsync();
        using var http = client;
        var catalogId = Guid.NewGuid();
        var key = Guid.NewGuid();
        await using (Taken(await TakeAsync(tenantId, userId, FormWorkLeaseKind.Export)))
        {
            using (var preview = await client.PostAsync(Catalogs + catalogId + "/imports/previews", Form(), Ct))
                (await AssertProblemAsync(preview, HttpStatusCode.Conflict, FormCatalogImportErrors.BusyCode)).Dispose();

            using var apply = new HttpRequestMessage(HttpMethod.Post, Catalogs + catalogId + "/imports") { Content = Form() };
            apply.Headers.Add("Idempotency-Key", key.ToString());
            using (var applied = await client.SendAsync(apply, Ct))
                (await AssertProblemAsync(applied, HttpStatusCode.Conflict, FormCatalogImportErrors.BusyCode)).Dispose();
            // La idempotencia no corrió: no leyó el formulario ni reservó la clave.
            Assert.Equal(0L, await ReservationsAsync(key));
        }

        using (var free = await client.PostAsync(Catalogs + catalogId + "/imports/previews", Form(), Ct))
            Assert.DoesNotContain(free.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.TooManyRequests });
        // El pedido que pasó soltó la barrera al terminar.
        await using var after = Taken(await TakeAsync(tenantId, userId, FormWorkLeaseKind.Preview));
    }

    [Fact]
    public async Task With_its_organization_full_the_route_answers_429_too_many_at_once_with_retry_after()
    {
        var (client, tenantId, _) = await BusinessAsync();
        using var http = client;
        await using var first = Taken(await TakeAsync(tenantId, Guid.NewGuid(), FormWorkLeaseKind.Preview));
        await using var second = Taken(await TakeAsync(tenantId, Guid.NewGuid(), FormWorkLeaseKind.Export));

        using var refused = await client.PostAsync(Catalogs + Guid.NewGuid() + "/imports/previews", Form(), Ct);
        using var problem = await AssertProblemAsync(refused, HttpStatusCode.TooManyRequests, FormCatalogImportErrors.TooManyAtOnceCode);
        Assert.Equal(TimeSpan.FromSeconds(FormWorkLeaseStore.RetryAfterSeconds), refused.Headers.RetryAfter?.Delta);
        Assert.Equal(FormWorkLeaseStore.RetryAfterSeconds, problem.RootElement.GetProperty("retryAfter").GetInt32());
    }

    private static MultipartFormDataContent Form() => new()
    {
        { new ByteArrayContent("code,text\nA,Uno\n"u8.ToArray()), "file", "opciones.csv" },
        { new StringContent("Append"), "mode" },
        { new StringContent("1"), "version" },
    };

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {body}");
        var problem = JsonDocument.Parse(body);
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        return problem;
    }

    private async Task<Result<IAsyncDisposable>> TakeAsync(Guid tenantId, Guid userId, FormWorkLeaseKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IFormWorkLease>().TryTakeAsync(userId, kind, Ct);
    }

    private static IAsyncDisposable Taken(Result<IAsyncDisposable> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private async Task<long> ReservationsAsync(Guid key)
    {
        await using var admin = new NpgsqlConnection(factory.AdminConnectionString);
        await admin.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""SELECT count(*) FROM platform."IdempotencyKeys" WHERE "Key" = @key""", admin);
        command.Parameters.AddWithValue("key", key);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task<(HttpClient Client, Guid TenantId, Guid UserId)> BusinessAsync()
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Barrera " + Guid.NewGuid().ToString("N")[..8], Ct);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ITenantFeatureRepository>().Add(
                    TenantFeature.Create(tenantId, "forms", true, TenantFeatureSource.Manual, null).Value);
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System,
                    Guid.Empty, tenantId, "Forms file slot fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        return (client, tenantId, owner.UserId);
    }
}
