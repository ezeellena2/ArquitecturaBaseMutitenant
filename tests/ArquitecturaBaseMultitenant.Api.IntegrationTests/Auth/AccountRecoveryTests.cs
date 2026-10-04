using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// «Recuperar mi cuenta» (ADR 0041): la respuesta no revela si un correo tiene cuenta, el código queda atado al
/// nonce del navegador, solo un método nuevo verificado registra el pedido y nunca se entrega una sesión.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountRecoveryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_verified_new_email_registers_a_pending_request_for_the_account_found_without_a_session()
    {
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        using var anonymous = Client();
        var newEmail = $"recover-{Guid.NewGuid():N}@example.test";
        var nonce = Nonce();

        using var requested = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = newEmail, nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.True((await JsonAsync(requested)).GetProperty("resendAfterSeconds").GetInt32() > 0);
        var code = await PickupCodeReader.ReadAsync(factory.Services, newEmail, Ct);

        using var otherNonce = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = newEmail, nonce = Nonce(), code }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, otherNonce.StatusCode);
        var invalid = await JsonAsync(otherNonce);
        Assert.Equal(LoginCodeErrors.InvalidCode, invalid.GetProperty("code").GetString());
        Assert.False(invalid.TryGetProperty(LoginCodeErrors.AttemptsLeftKey, out _));
        using var withoutVerification = await AccountJourney.PostAsync(anonymous, "/api/account-recovery",
            new { email = newEmail, nonce, previousEmail = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutVerification.StatusCode);
        Assert.Equal(AccountRecoveryErrors.VerificationRequiredCode, await CodeAsync(withoutVerification));
        using var verified = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = newEmail, nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);

        var before = await OutboxIdsAsync();
        var key = Guid.NewGuid().ToString("D");
        var body = new { email = newEmail, nonce, previousEmail = account.Email.Value };
        using var submitted = await SendAsync(anonymous, "/api/account-recovery", body, key);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        Assert.False(submitted.Headers.Contains("Set-Cookie"));
        using var replayed = await SendAsync(anonymous, "/api/account-recovery", body, key);
        Assert.Equal(HttpStatusCode.Accepted, replayed.StatusCode);
        Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
        using var reused = await AccountJourney.PostAsync(anonymous, "/api/account-recovery", body, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal(AccountRecoveryErrors.VerificationRequiredCode, await CodeAsync(reused));
        using var me = await anonymous.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var request = Assert.Single(await db.AccountRecoveryRequests.AsNoTracking()
            .Where(item => item.NewMethodValue == newEmail).ToArrayAsync(Ct));
        Assert.Equal(AccountRecoveryStatus.Pending, request.Status);
        Assert.Equal(account.UserId, request.UserId);
        Assert.Equal(LoginMethodType.Email, request.PreviousMethodType);
        Assert.Equal(account.Email.Value, request.PreviousMethodValue);
        Assert.Equal(LoginMethodType.Email, request.NewMethodType);
        Assert.False(await db.LoginMethods.AnyAsync(method => method.Value == newEmail, Ct));
        var received = Assert.Single(await EmailsToAsync(services, newEmail, before));
        Assert.Equal("Recibimos tu pedido para recuperar tu cuenta", received.Subject);
        Assert.Contains("Lo revisa una persona de la plataforma", received.TextBody);
    }

    [Fact]
    public async Task A_new_email_that_already_has_an_account_gets_a_notice_instead_of_a_code_with_the_same_response()
    {
        using var person = Client();
        var owner = await AccountJourney.RegisterAsync(factory, person, Ct);
        // Los límites por destino se comparten con el código del registro recién pedido.
        await AgeLoginCodesAsync(owner.Email.Value);
        using var anonymous = Client();
        var before = await OutboxIdsAsync();
        var takenNonce = Nonce();
        var freeEmail = $"recover-{Guid.NewGuid():N}@example.test";
        var freeNonce = Nonce();

        using var taken = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = owner.Email.Value, nonce = takenNonce }, Ct);
        using var free = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = freeEmail, nonce = freeNonce }, Ct);

        Assert.Equal(free.StatusCode, taken.StatusCode);
        Assert.Equal(await free.Content.ReadAsStringAsync(Ct), await taken.Content.ReadAsStringAsync(Ct));
        // Pedir otro enseguida da la misma espera en los dos casos.
        using var takenAgain = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = owner.Email.Value, nonce = takenNonce }, Ct);
        using var freeAgain = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = freeEmail, nonce = freeNonce }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, takenAgain.StatusCode);
        Assert.Equal(freeAgain.StatusCode, takenAgain.StatusCode);
        var takenWait = await JsonAsync(takenAgain);
        var freeWait = await JsonAsync(freeAgain);
        Assert.Equal(freeWait.GetProperty("code").GetString(), takenWait.GetProperty("code").GetString());
        Assert.InRange(freeWait.GetProperty(LoginCodeErrors.RetryAfterKey).GetInt32()
            - takenWait.GetProperty(LoginCodeErrors.RetryAfterKey).GetInt32(), -1, 1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var notice = Assert.Single(await EmailsToAsync(services, owner.Email.Value, before));
            Assert.Equal("Ya tenés una cuenta con este correo", notice.Subject);
            Assert.Contains("no mandamos ningún código", notice.TextBody);
            Assert.DoesNotMatch(new Regex(@"(?<!\d)\d{6}(?!\d)"), notice.TextBody);
            var db = services.GetRequiredService<ApplicationDbContext>();
            var row = await db.LoginCodes.AsNoTracking().SingleAsync(item =>
                item.Destination == owner.Email.Value && item.Purpose == LoginCodePurpose.AccountRecovery, Ct);
            Assert.Null(row.SentAtUtc);
            Assert.NotNull(row.NonceHash);
            Assert.True(await db.OutboxMessages.AnyAsync(message => message.UserId == owner.UserId && !before.Contains(message.Id), Ct));
        }

        using var guessed = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = owner.Email.Value, nonce = takenNonce, code = "123456" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, guessed.StatusCode);
        var problem = await JsonAsync(guessed);
        Assert.Equal(LoginCodeErrors.InvalidCode, problem.GetProperty("code").GetString());
        // Igual que un código mandado y mal escrito: los intentos restantes no delatan que no hubo código.
        Assert.Equal(4, problem.GetProperty(LoginCodeErrors.AttemptsLeftKey).GetInt32());
        var freeCode = await PickupCodeReader.ReadAsync(factory.Services, freeEmail, Ct);
        using var mistyped = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = freeEmail, nonce = freeNonce, code = OtherCode(freeCode) }, Ct);
        Assert.Equal(mistyped.StatusCode, guessed.StatusCode);
        Assert.Equal(WithoutTrace(await JsonAsync(mistyped)), WithoutTrace(problem));
    }

    [Fact]
    public async Task A_google_contact_email_counts_as_an_account_and_gets_the_notice()
    {
        using var person = Client();
        var owner = await AccountJourney.RegisterAsync(factory, person, Ct);
        var contact = $"contact-{Guid.NewGuid():N}@example.test";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var google = LoginMethod.CreateGoogle(owner.UserId, "google-" + Guid.NewGuid().ToString("N"),
                    Email.Create(contact).Value);
                Assert.True(google.Verify(now).IsSuccess);
                services.GetRequiredService<ApplicationDbContext>().LoginMethods.Add(google);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        using var anonymous = Client();
        var before = await OutboxIdsAsync();
        using var requested = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = contact, nonce = Nonce() }, Ct);

        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var notice = Assert.Single(await EmailsToAsync(check.ServiceProvider, contact, before));
        Assert.Equal("Ya tenés una cuenta con este correo", notice.Subject);
    }

    [Fact]
    public async Task Two_simultaneous_submissions_with_the_same_code_register_a_single_request()
    {
        using var anonymous = Client();
        var newEmail = $"recover-{Guid.NewGuid():N}@example.test";
        var nonce = Nonce();
        using var requested = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = newEmail, nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, newEmail, Ct);
        using var verified = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = newEmail, nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);

        var body = new { email = newEmail, nonce, previousEmail = "antes-" + newEmail };
        var responses = await Task.WhenAll(
            SendAsync(anonymous, "/api/account-recovery", body, Guid.NewGuid().ToString("D")),
            SendAsync(anonymous, "/api/account-recovery", body, Guid.NewGuid().ToString("D")));
        try
        {
            Assert.Equal(new[] { HttpStatusCode.Accepted, HttpStatusCode.BadRequest },
                responses.Select(response => response.StatusCode).Order().ToArray());
            Assert.Equal(AccountRecoveryErrors.VerificationRequiredCode,
                await CodeAsync(responses.Single(response => response.StatusCode == HttpStatusCode.BadRequest)));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests
            .CountAsync(item => item.NewMethodValue == newEmail, Ct));
    }

    [Fact]
    public async Task An_unknown_previous_phone_registers_the_request_without_an_account_and_the_contract_is_validated()
    {
        using var anonymous = Client();
        var newEmail = $"recover-{Guid.NewGuid():N}@example.test";
        var nonce = Nonce();
        using var requested = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = newEmail, nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, newEmail, Ct);
        using var verified = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = newEmail, nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        var before = await OutboxIdsAsync();

        using var sameEmail = await AccountJourney.PostAsync(anonymous, "/api/account-recovery",
            new { email = newEmail, nonce, previousEmail = newEmail }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, sameEmail.StatusCode);
        Assert.Equal("Tiene que ser un correo distinto del de antes.",
            (await JsonAsync(sameEmail)).GetProperty("errors").GetProperty("previousEmail")[0].GetString());
        using var both = await AccountJourney.PostAsync(anonymous, "/api/account-recovery",
            new { email = newEmail, nonce, previousEmail = "otra@example.test", previousPhone = new { country = "AR", number = "11 2345-6789" } }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
        using var badNonce = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = newEmail, nonce = "corto" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badNonce.StatusCode);
        Assert.True((await JsonAsync(badNonce)).GetProperty("errors").TryGetProperty("nonce", out _));
        using var form = new HttpRequestMessage(HttpMethod.Post, "/api/account-recovery/code")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = newEmail, ["nonce"] = nonce }),
        };
        form.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var formResponse = await anonymous.SendAsync(form, Ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, formResponse.StatusCode);

        using var submitted = await AccountJourney.PostAsync(anonymous, "/api/account-recovery",
            new { email = newEmail, nonce, previousPhone = new { country = "AR", number = "11 2345-6789" } }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var request = await services.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests.AsNoTracking()
            .SingleAsync(item => item.NewMethodValue == newEmail, Ct);
        Assert.Null(request.UserId);
        Assert.Equal(LoginMethodType.Phone, request.PreviousMethodType);
        Assert.Equal("+5491123456789", request.PreviousMethodValue);
        Assert.Equal("Recibimos tu pedido para recuperar tu cuenta",
            Assert.Single(await EmailsToAsync(services, newEmail, before)).Subject);
    }

    private async Task AgeLoginCodesAsync(string email)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
            WHERE "Destination" = @email
            """, connection);
        command.Parameters.AddWithValue("email", email);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private HttpClient Client() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static string OtherCode(string code) => code == "000000" ? "111111" : "000000";

    /// <summary>El problema sin el traceId, que cambia en cada pedido.</summary>
    private static string WithoutTrace(JsonElement problem) =>
        JsonSerializer.Serialize(problem.EnumerateObject().Where(property => property.Name != "traceId")
            .ToDictionary(property => property.Name, property => property.Value.ToString()));

    private static string Nonce() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string path, object body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await JsonAsync(response)).GetProperty("code").GetString();

    private async Task<Guid[]> OutboxIdsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => message.Id).ToArrayAsync(Ct);
    }

    /// <summary>Los correos que se encolaron para un destinatario después de <paramref name="before"/>.</summary>
    private static async Task<EmailMessage[]> EmailsToAsync(IServiceProvider services, string to, Guid[] before)
    {
        var protector = services.GetRequiredService<IPayloadProtector>();
        var payloads = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(message => message.Channel == "email" && !before.Contains(message.Id))
            .Select(message => message.EncryptedPayload).ToArrayAsync(Ct);
        return payloads.Select(payload => JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(payload))!)
            .Where(message => string.Equals(message.To, to, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
