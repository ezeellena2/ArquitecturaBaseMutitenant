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
    public async Task An_open_session_cannot_add_email_without_reauthentication()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        using var added = await AccountJourney.PostAsync(client, "/api/me/login-methods",
            new { email = "unproved-" + Guid.NewGuid().ToString("N") + "@example.test" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, added.StatusCode);
        using var listed = await client.GetAsync("/api/me/login-methods", Ct);
        using var body = await listed.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Single(body!.RootElement.GetProperty("methods").EnumerateArray());
    }

    [Fact]
    public async Task A_method_verified_after_sign_in_cannot_authorize_removing_the_original()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        Guid originalId;
        LoginMethod laterMethod;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            originalId = (await services.GetRequiredService<ILoginMethodReader>().ListByUserIdAsync(account.UserId, Ct)).Single().Id;
            laterMethod = LoginMethod.CreateEmail(account.UserId,
                Email.Create("later-" + Guid.NewGuid().ToString("N") + "@example.test").Value);
            laterMethod.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddSeconds(1));
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<ILoginMethodRepository>().Add(laterMethod);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var challenge = await AccountJourney.PostAsync(client, "/api/me/reauth",
            new { action = "RemoveMethod", targetMethodId = originalId }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, challenge.StatusCode);
        using var problem = await challenge.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(ReauthErrors.OtherMethodRequired.Code, problem!.RootElement.GetProperty("code").GetString());

        await using var codeScope = factory.Services.CreateAsyncScope();
        var codeServices = codeScope.ServiceProvider;
        var context = codeServices.GetRequiredService<ApplicationDbContext>();
        var work = codeServices.GetRequiredService<IUnitOfWork>();
        var hasher = codeServices.GetRequiredService<ILoginCodeHasher>();
        var nowUtc = codeServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var destination = LoginCodeDestination.ForEmail(Email.Create(laterMethod.Value).Value);
        await work.ExecuteInTransactionAsync(ct =>
        {
            var code = LoginCode.Issue(destination, LoginCodePurpose.Reauthenticate, account.UserId,
                hasher.Hash(destination, LoginCodePurpose.Reauthenticate, "123456"), nowUtc,
                TimeSpan.FromMinutes(10), 5);
            code.BindReauthentication(ReauthAction.RemoveMethod, laterMethod.Id, originalId);
            context.LoginCodes.Add(code);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using var forged = await AccountJourney.PostAsync(client, "/api/me/reauth/verify",
            new { action = "RemoveMethod", targetMethodId = originalId, sourceMethodId = laterMethod.Id, code = "123456" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        using var forgedProblem = await forged.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(ReauthErrors.Invalid.Code, forgedProblem!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Adding_an_email_of_another_account_answers_like_a_free_one_and_only_its_owner_hears()
    {
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AgeCodesAsync(owner.Email.Value);
        var free = "free-" + Guid.NewGuid().ToString("N") + "@example.test";
        var before = await OutboxIdsAsync();

        using var freeAdded = await AddEmailAsync(client, account, free);
        using var takenAdded = await AddEmailAsync(client, account, owner.Email.Value);

        // ADR 0041: la misma respuesta y la misma espera para reenviar.
        Assert.Equal(HttpStatusCode.Accepted, freeAdded.StatusCode);
        Assert.Equal(freeAdded.StatusCode, takenAdded.StatusCode);
        using var freeBody = await freeAdded.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        using var takenBody = await takenAdded.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(freeBody!.RootElement.EnumerateObject().Select(property => property.Name),
            takenBody!.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(freeBody.RootElement.GetProperty("resendAfterSeconds").GetInt32(),
            takenBody.RootElement.GetProperty("resendAfterSeconds").GetInt32());
        using var freeAgain = await AddEmailAsync(client, account, free);
        using var takenAgain = await AddEmailAsync(client, account, owner.Email.Value);
        Assert.Equal(HttpStatusCode.TooManyRequests, takenAgain.StatusCode);
        Assert.Equal(freeAgain.StatusCode, takenAgain.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        // Al dueño no le llega ningún código, solo el aviso; el correo ocupado no se suma a la otra cuenta.
        var notice = Assert.Single(await EmailsToAsync(services, owner.Email.Value, before));
        Assert.Equal("Ya tenés una cuenta con este correo", notice.Subject);
        Assert.Contains("Alguien quiso agregar este correo para entrar a otra cuenta de", notice.TextBody);
        Assert.Contains("Si fuiste vos, entrá con este correo: ya tenés una cuenta.", notice.TextBody);
        Assert.Contains("Si no fuiste vos, ignorá este mensaje.", notice.TextBody);
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex(@"(?<!\d)\d{6}(?!\d)"), notice.TextBody);
        Assert.False(await db.LoginCodes.AnyAsync(code => code.Destination == owner.Email.Value
            && code.Purpose == LoginCodePurpose.VerifyDestination && code.SentAtUtc != null, Ct));
        Assert.False(await db.LoginMethods.AnyAsync(method => method.UserId == account.UserId
            && method.Value == owner.Email.Value, Ct));
        Assert.Single(await EmailsToAsync(services, free, before));
    }

    /// <summary>Agrega un correo en Mi cuenta con un comprobante de reautenticación nuevo, como la pantalla.</summary>
    private async Task<HttpResponseMessage> AddEmailAsync(HttpClient client, AccountJourney account, string email)
    {
        string ticket;
        await using (var scope = factory.Services.CreateAsyncScope())
            ticket = await AccountJourney.IssueReauthTicketAsync(scope.ServiceProvider, account.UserId,
                ReauthAction.AddEmail, Ct);
        return await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email, reauthTicket = ticket }, Ct);
    }

    private async Task AgeCodesAsync(string destination) =>
        await RecoveryJourney.AgeCodesAsync(factory, destination, Ct);

    private async Task<Guid[]> OutboxIdsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => message.Id).ToArrayAsync(Ct);
    }

    private static async Task<EmailMessage[]> EmailsToAsync(IServiceProvider services, string to, Guid[] before)
    {
        var protector = services.GetRequiredService<IPayloadProtector>();
        var payloads = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(message => message.Channel == "email" && !before.Contains(message.Id))
            .Select(message => message.EncryptedPayload).ToArrayAsync(Ct);
        return [.. payloads.Select(payload => JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(payload))!)
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
        var reauthTicket = await AccountJourney.IssueReauthTicketAsync(scope.ServiceProvider, account.UserId, ReauthAction.AddEmail, Ct);
        var email = "http-added-" + Guid.NewGuid().ToString("N") + "@example.test";
        var key = Guid.NewGuid().ToString("D");
        var methodId = Guid.Empty;
        for (var replay = 0; replay < 2; replay++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/login-methods")
                { Content = JsonContent.Create(new { email, reauthTicket }) };
            request.Headers.Add("Idempotency-Key", key);
            using var added = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
            using var body = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            var returnedId = body!.RootElement.GetProperty("methodId").GetGuid();
            if (replay == 0) methodId = returnedId;
            Assert.Equal(methodId, returnedId);
        }
        var freshTicket = await AccountJourney.IssueReauthTicketAsync(scope.ServiceProvider, account.UserId, ReauthAction.AddEmail, Ct);
        // Volver a agregar el mismo correo pendiente es pedir otro código: respeta la espera para reenviar.
        using var duplicate = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email, reauthTicket = freshTicket }, Ct);
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
