using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba los contratos HTTP de gestión de métodos de ingreso con sesiones reales. Cubre prueba de
/// titularidad, recursos ajenos y repetición idempotente de solicitudes.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountMethodsHttpTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly Dictionary<Guid, EmailMessage> observedPayloads = [];

    [Fact]
    public async Task Adding_a_verified_email_of_another_account_rejects_without_a_pending_method_code_or_notice()
    {
        var (owner, client, account) = await TwoAccountsAsync();
        var free = "free-" + Guid.NewGuid().ToString("N") + "@example.test";
        var before = await OutboxIdsAsync();
        using var freeAdded = await AddEmailAsync(client, account, free);
        Assert.Equal(HttpStatusCode.Accepted, freeAdded.StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var takenAdded = await AddEmailAsync(client, account, owner.Email.Value);
            Assert.Equal(HttpStatusCode.BadRequest, takenAdded.StatusCode);
            var problem = await takenAdded.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(LoginMethodErrors.AlreadyUsedCode, problem.GetProperty("code").GetString());
            Assert.Equal("Ese correo ya está asociado a otra cuenta.", problem.GetProperty("errors").GetProperty("email")[0].GetString());
        }
        using var freeAgain = await AddEmailAsync(client, account, free);
        Assert.Equal(HttpStatusCode.TooManyRequests, freeAgain.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await EmailsToAsync(services, owner.Email.Value, before));
        Assert.False(await db.LoginCodes.AnyAsync(code => code.Destination == owner.Email.Value && code.Purpose == LoginCodePurpose.VerifyDestination, Ct));
        Assert.False(await db.LoginMethods.AnyAsync(method => method.UserId == account.UserId && method.Value == owner.Email.Value, Ct));
        Assert.Single(await EmailsToAsync(services, free, before));
    }

    // La reserva pendiente ajena sigue siendo recuperable; sus códigos conservan intentos, reenvío y vencimiento.
    [Fact]
    public async Task An_unverified_reservation_is_reclaimed_and_its_code_keeps_attempts_and_resends()
    {
        var (owner, client, account) = await TwoAccountsAsync();
        var free = "free-" + Guid.NewGuid().ToString("N") + "@example.test";
        var taken = "reserved-" + Guid.NewGuid().ToString("N") + "@example.test";
        await SeedPendingAsync(owner.UserId, taken);
        var before = await OutboxIdsAsync();
        var freeId = await AddedMethodIdAsync(client, account, free);
        var takenId = await AddedMethodIdAsync(client, account, taken);

        // La lista: los dos pendientes, iguales salvo el id y el correo.
        var (freeRow, takenRow) = await RowsAsync(client, freeId, takenId);
        Assert.Equal(free, freeRow.GetProperty("value").GetString());
        Assert.Equal(taken, takenRow.GetProperty("value").GetString());
        Assert.False(takenRow.GetProperty("isVerified").GetBoolean());
        AssertSameRow(freeRow, takenRow);

        await EmailsToAsync(factory.Services, free, before);
        await EmailsToAsync(factory.Services, taken, before);

        // Verificar con un código equivocado: la misma respuesta, y los dos cuentan el intento.
        var freeCode = await PickupCodeReader.ReadAsync(factory.Services, free, Ct);
        var takenCode = await PickupCodeReader.ReadAsync(factory.Services, taken, Ct);
        var wrong = new[] { "000000", "111111", "222222" }.First(value => value != freeCode && value != takenCode);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var freeWrong = await VerifyAsync(client, freeId, wrong);
            using var takenWrong = await VerifyAsync(client, takenId, wrong);
            Assert.Equal(HttpStatusCode.BadRequest, freeWrong.StatusCode);
            await AssertSameProblemAsync(freeWrong, takenWrong);
        }

        // Reenviar conserva la respuesta, el método y la espera de ambos códigos reales.
        await AgeCodesAsync(free);
        await AgeCodesAsync(taken);
        using var freeResent = await AccountJourney.PostAsync(client, $"/api/me/login-methods/{freeId:D}/code", new { }, Ct);
        using var takenResent = await AccountJourney.PostAsync(client, $"/api/me/login-methods/{takenId:D}/code", new { }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, freeResent.StatusCode);
        Assert.Equal(freeResent.StatusCode, takenResent.StatusCode);
        using var freeResentBody = await freeResent.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        using var takenResentBody = await takenResent.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(freeId, freeResentBody!.RootElement.GetProperty("methodId").GetGuid());
        Assert.Equal(takenId, takenResentBody!.RootElement.GetProperty("methodId").GetGuid());
        Assert.Equal(freeResentBody.RootElement.GetProperty("resendAfterSeconds").GetInt32(),
            takenResentBody.RootElement.GetProperty("resendAfterSeconds").GetInt32());

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Assert.Equal(2, (await EmailsToAsync(services, free, before)).Length);
        Assert.Equal(2, (await EmailsToAsync(services, taken, before)).Length);
        var db = services.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.LoginCodes.AnyAsync(code => code.Destination == taken
            && code.Purpose == LoginCodePurpose.VerifyDestination && code.SentAtUtc != null, Ct));
        Assert.False(await db.LoginMethods.AnyAsync(method => method.UserId == owner.UserId && method.Value == taken, Ct));
        Assert.Equal(1, await db.LoginMethods.CountAsync(method => method.UserId == account.UserId
            && method.Value == taken && method.VerifiedAtUtc == null, Ct));
    }

    // El pendiente recuperado se quita y vence como cualquier otro.
    [Fact]
    public async Task A_reclaimed_pending_email_is_removed_and_expires_like_a_free_one()
    {
        var (owner, client, account) = await TwoAccountsAsync();
        var free = "free-" + Guid.NewGuid().ToString("N") + "@example.test";
        var taken = "reserved-" + Guid.NewGuid().ToString("N") + "@example.test";
        await SeedPendingAsync(owner.UserId, taken);
        var freeId = await AddedMethodIdAsync(client, account, free);
        var takenId = await AddedMethodIdAsync(client, account, taken);

        using var freeRemoved = await RemoveAsync(client, account, freeId);
        using var takenRemoved = await RemoveAsync(client, account, takenId);
        Assert.Equal(HttpStatusCode.NoContent, freeRemoved.StatusCode);
        Assert.Equal(freeRemoved.StatusCode, takenRemoved.StatusCode);
        Assert.DoesNotContain(await MethodIdsAsync(client), id => id == freeId || id == takenId);

        // Agregados otra vez y vencidos sus códigos: verificar responde igual y los dos salen de la lista.
        await AgeCodesAsync(free);
        await AgeCodesAsync(taken);
        freeId = await AddedMethodIdAsync(client, account, free);
        takenId = await AddedMethodIdAsync(client, account, taken);
        await ExpireCodesAsync(account.UserId, free);
        await ExpireCodesAsync(account.UserId, taken);
        using var freeExpired = await VerifyAsync(client, freeId, "123456");
        using var takenExpired = await VerifyAsync(client, takenId, "123456");
        Assert.Equal(HttpStatusCode.BadRequest, freeExpired.StatusCode);
        await AssertSameProblemAsync(freeExpired, takenExpired);
        Assert.DoesNotContain(await MethodIdsAsync(client), id => id == freeId || id == takenId);
    }

    private async Task SeedPendingAsync(Guid userId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ILoginMethodRepository>().Add(LoginMethod.CreateEmail(userId, Email.Create(email).Value));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task<(AccountJourney Owner, HttpClient Client, AccountJourney Account)> TwoAccountsAsync()
    {
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        await AgeCodesAsync(owner.Email.Value);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        return (owner, client, await AccountJourney.RegisterAsync(factory, client, Ct));
    }

    private static async Task<Guid> AddedMethodIdAsync(HttpClient client, AccountJourney account, string email)
    {
        using var added = await AddEmailAsync(client, account, email);
        Assert.True(added.StatusCode == HttpStatusCode.Accepted, await added.Content.ReadAsStringAsync(Ct));
        using var body = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return body!.RootElement.GetProperty("methodId").GetGuid();
    }

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, Guid methodId, string code) =>
        AccountJourney.PostAsync(client, $"/api/me/login-methods/{methodId:D}/verify", new { code }, Ct);

    private static async Task<HttpResponseMessage> RemoveAsync(HttpClient client, AccountJourney account, Guid methodId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/me/login-methods/{methodId:D}");
        return await client.SendAsync(request, Ct);
    }

    private static async Task<(JsonElement Free, JsonElement Taken)> RowsAsync(HttpClient client, Guid freeId, Guid takenId)
    {
        using var listed = await client.GetAsync("/api/me/login-methods", Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var list = await listed.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var rows = list!.RootElement.GetProperty("methods").EnumerateArray().Select(row => row.Clone()).ToArray();
        return (Assert.Single(rows, row => row.GetProperty("id").GetGuid() == freeId),
            Assert.Single(rows, row => row.GetProperty("id").GetGuid() == takenId));
    }

    private static async Task<Guid[]> MethodIdsAsync(HttpClient client)
    {
        using var listed = await client.GetAsync("/api/me/login-methods", Ct);
        using var list = await listed.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return [.. list!.RootElement.GetProperty("methods").EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }

    private static void AssertSameRow(JsonElement free, JsonElement taken)
    {
        foreach (var property in free.EnumerateObject().Where(property => property.Name is not ("id" or "value")))
            Assert.Equal(property.Value.GetRawText(), taken.GetProperty(property.Name).GetRawText());
    }

    private static async Task AssertSameProblemAsync(HttpResponseMessage free, HttpResponseMessage taken)
    {
        Assert.Equal(free.StatusCode, taken.StatusCode);
        using var freeProblem = await free.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        using var takenProblem = await taken.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        foreach (var property in freeProblem!.RootElement.EnumerateObject()
                     .Where(property => property.Name is not ("traceId" or "instance")))
            Assert.Equal(property.Value.GetRawText(), takenProblem!.RootElement.GetProperty(property.Name).GetRawText());
    }

    private async Task ExpireCodesAsync(Guid userId, string destination)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            UPDATE identity."LoginCodes" SET "ExpiresAtUtc" = now() - interval '1 minute'
            WHERE "Destination" = @destination AND "RequestedByUserId" = @userId
            """, connection);
        command.Parameters.AddWithValue("destination", destination);
        command.Parameters.AddWithValue("userId", userId);
        Assert.True(await command.ExecuteNonQueryAsync(Ct) > 0);
    }

    [Fact]
    public async Task An_account_can_still_add_the_email_of_its_own_google_sign_in()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var contact = "google-contact-" + Guid.NewGuid().ToString("N") + "@example.test";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var google = LoginMethod.CreateGoogle(account.UserId, "google-" + Guid.NewGuid().ToString("N"),
                    Email.Create(contact).Value);
                Assert.True(google.Verify(nowUtc).IsSuccess);
                services.GetRequiredService<ApplicationDbContext>().LoginMethods.Add(google);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var before = await OutboxIdsAsync();

        // El contacto de su propio Google no es «de otra cuenta»: se suma como correo, con su código.
        using var added = await AddEmailAsync(client, account, contact);

        Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var message = Assert.Single(await EmailsToAsync(check.ServiceProvider, contact, before));
        Assert.NotEqual("Ya tenés una cuenta con este correo", message.Subject);
        Assert.True(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.AnyAsync(
            method => method.UserId == account.UserId && method.Type == LoginMethodType.Email
                && method.Value == contact && method.VerifiedAtUtc == null, Ct));
    }

    /// <summary>Agrega un correo desde la sesión; sólo el destino nuevo requiere código.</summary>
    private static Task<HttpResponseMessage> AddEmailAsync(HttpClient client, AccountJourney account, string email) =>
        AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct);

    private async Task AgeCodesAsync(string destination) =>
        await RecoveryJourney.AgeCodesAsync(factory, destination, Ct);

    private async Task<Guid[]> OutboxIdsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => message.Id).ToArrayAsync(Ct);
    }

    private async Task<EmailMessage[]> EmailsToAsync(IServiceProvider services, string to, Guid[] before)
    {
        var protector = services.GetRequiredService<IPayloadProtector>();
        var payloads = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(message => message.Channel == "email" && !before.Contains(message.Id))
            .Select(message => new { message.Id, message.EncryptedPayload }).ToArrayAsync(Ct);
        foreach (var payload in payloads.Where(value => value.EncryptedPayload.Length > 0))
            observedPayloads[payload.Id] = JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(payload.EncryptedPayload))!;
        return [.. observedPayloads.Where(value => !before.Contains(value.Key)).Select(value => value.Value)
            .Where(message => string.Equals(message.To, to, StringComparison.OrdinalIgnoreCase))];
    }

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
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
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
        // Volver a agregar el mismo correo pendiente es pedir otro código: respeta la espera para reenviar.
        using var duplicate = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, duplicate.StatusCode);
        using var duplicateProblem = await duplicate.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginCodeErrors.ResendTooSoonCode, duplicateProblem!.RootElement.GetProperty("code").GetString());
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
