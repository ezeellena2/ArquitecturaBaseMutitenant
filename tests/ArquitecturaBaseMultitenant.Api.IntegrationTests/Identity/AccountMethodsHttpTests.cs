using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba los contratos HTTP de gestión de métodos de ingreso con sesiones reales. Cubre prueba de
/// titularidad, recursos ajenos y repetición idempotente de solicitudes.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountMethodsHttpTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Methods_require_authentication_and_foreign_method_returns_404()
    {
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/me/login-methods", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        var foreignMethodId = Guid.Empty;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var owner = await services.GetRequiredService<IUserRepository>().CreateAsync(null,
                    "es-AR", "America/Argentina/Buenos_Aires", ct);
                var method = LoginMethod.CreateEmail(owner.Id, Email.Create(owner.Id.ToString("N") + "@example.test").Value);
                foreignMethodId = method.Id;
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var foreign = await AccountJourney.PostAsync(client, "/api/me/login-methods/" + foreignMethodId.ToString("D") + "/verify",
            new { code = "123456" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var problem = await foreign.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginMethodErrors.NotFound.Code, problem!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Add_verify_and_list_exercise_real_contracts_and_idempotent_replay()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        var email = "http-added-" + Guid.NewGuid().ToString("N") + "@example.test";
        var key = Guid.NewGuid().ToString("D");
        var methodId = Guid.Empty;
        for (var replay = 0; replay < 2; replay++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/login-methods")
                { Content = JsonContent.Create(new { email }) };
            request.Headers.Add("Idempotency-Key", key);
            using var added = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
            using var body = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            var returnedId = body!.RootElement.GetProperty("methodId").GetGuid();
            if (replay == 0) methodId = returnedId;
            Assert.Equal(methodId, returnedId);
        }
        using var duplicate = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var duplicateProblem = await duplicate.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Contains("correo", duplicateProblem!.RootElement.GetProperty("errors").GetProperty("email")[0].GetString());
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var verified = await AccountJourney.PostAsync(client,
            "/api/me/login-methods/" + methodId.ToString("D") + "/verify", new { code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var listed = await client.GetAsync("/api/me/login-methods", Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var list = await listed.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var rows = list!.RootElement.GetProperty("methods").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        var row = rows.Single(row => row.GetProperty("id").GetGuid() == methodId);
        Assert.Equal(email, row.GetProperty("value").GetString());
        Assert.True(row.GetProperty("isVerified").GetBoolean());
        Assert.True(row.GetProperty("canRemove").GetBoolean());
        Assert.True(row.GetProperty("canMakePrimary").GetBoolean());
    }
}
