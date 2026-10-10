using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// «Recuperar mi cuenta» (ADR 0043): valida la cuenta anterior y propiedad del nuevo correo, conserva el nonce,
/// la verificación y el uso único del código, y nunca entrega una sesión.
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
    public async Task A_new_email_that_already_has_an_account_is_rejected_without_a_code_or_notice()
    {
        using var person = Client();
        var owner = await AccountJourney.RegisterAsync(factory, person, Ct);
        await AgeLoginCodesAsync(owner.Email.Value);
        using var anonymous = Client();
        var before = await OutboxIdsAsync();
        var takenNonce = Nonce();
        var freeEmail = $"recover-{Guid.NewGuid():N}@example.test";
        var freeNonce = Nonce();
        using var taken = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = owner.Email.Value, nonce = takenNonce }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        var rejected = await JsonAsync(taken);
        Assert.Equal(AccountRecoveryErrors.MethodInUseCode, rejected.GetProperty("code").GetString());
        Assert.Equal("Ese método de ingreso ya es de otra cuenta.", rejected.GetProperty("errors").GetProperty("email")[0].GetString());
        using var free = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = freeEmail, nonce = freeNonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, free.StatusCode);
        using var freeAgain = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code",
            new { email = freeEmail, nonce = freeNonce }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, freeAgain.StatusCode);
        Assert.True((await JsonAsync(freeAgain)).GetProperty(LoginCodeErrors.RetryAfterKey).GetInt32() > 0);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Assert.Empty(await EmailsToAsync(services, owner.Email.Value, before));
        Assert.False(await services.GetRequiredService<ApplicationDbContext>().LoginCodes.AnyAsync(item =>
            item.Destination == owner.Email.Value && item.Purpose == LoginCodePurpose.AccountRecovery, Ct));
        using var guessed = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = owner.Email.Value, nonce = takenNonce, code = "123456" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, guessed.StatusCode);
        Assert.Equal(LoginCodeErrors.InvalidCode, await CodeAsync(guessed));
        var freeCode = await PickupCodeReader.ReadAsync(factory.Services, freeEmail, Ct);
        using var mistyped = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify",
            new { email = freeEmail, nonce = freeNonce, code = OtherCode(freeCode) }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, mistyped.StatusCode);
        Assert.Equal(4, (await JsonAsync(mistyped)).GetProperty(LoginCodeErrors.AttemptsLeftKey).GetInt32());
    }

    [Fact]
    public async Task A_google_contact_email_is_owned_for_recovery_and_personal_signup()
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

        Assert.Equal(HttpStatusCode.BadRequest, requested.StatusCode);
        Assert.Equal(AccountRecoveryErrors.MethodInUseCode, await CodeAsync(requested));
        using var signup = await AccountJourney.PostAsync(anonymous, "/api/auth/signup", new { email = contact, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, signup.StatusCode);
        Assert.Equal("Auth.Signup.EmailTaken", await CodeAsync(signup));
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Empty(await EmailsToAsync(check.ServiceProvider, contact, before));
        Assert.False(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes.AnyAsync(code => code.Destination == contact, Ct));
    }

    [Fact]
    public async Task Two_simultaneous_submissions_with_the_same_code_register_a_single_request()
    {
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
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

        var body = new { email = newEmail, nonce, previousEmail = account.Email.Value };
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
    public async Task An_unknown_previous_phone_rejects_without_a_request_and_the_contract_is_validated()
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
        Assert.Equal(HttpStatusCode.BadRequest, submitted.StatusCode);
        Assert.Equal(AccountRecoveryErrors.PhoneAccountNotFoundCode, await CodeAsync(submitted));
        Assert.True((await JsonAsync(submitted)).GetProperty("errors").TryGetProperty("previousPhone", out _));

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Assert.False(await services.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests.AsNoTracking()
            .AnyAsync(item => item.NewMethodValue == newEmail, Ct));
        Assert.Empty(await EmailsToAsync(services, newEmail, before));
    }

    [Fact]
    public async Task Submitting_revalidates_the_previous_account_and_only_the_existing_account_hears_afterwards()
    {
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        using var anonymous = Client();
        var forAccount = $"recover-{Guid.NewGuid():N}@example.test";
        var forNobody = $"recover-{Guid.NewGuid():N}@example.test";

        var withAccount = await SubmitCountingWritesAsync(anonymous, forAccount, account.Email.Value);
        var withoutAccount = await SubmitCountingWritesAsync(anonymous, forNobody, $"nadie-{Guid.NewGuid():N}@example.test", accountExists: false);

        // El Submit no permite saltear Preview; el aviso legítimo a la cuenta se encola después.
        Assert.Equal((Requests: 1, Messages: 1), withAccount);
        Assert.Equal((Requests: 0, Messages: 0), withoutAccount);
        var before = await OutboxIdsAsync();
        await Task.WhenAll(
            ActivatorUtilities.CreateInstance<AccountRecoveryNoticeWorker>(factory.Services).RunOnceAsync(Ct),
            ActivatorUtilities.CreateInstance<AccountRecoveryNoticeWorker>(factory.Services).RunOnceAsync(Ct));

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var warning = Assert.Single(await EmailsToAsync(services, account.Email.Value, before));
        Assert.Equal("Alguien pidió recuperar tu cuenta", warning.Subject);
        Assert.DoesNotContain(forAccount, warning.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await EmailsToAsync(services, forAccount, before));
        Assert.Empty(await EmailsToAsync(services, forNobody, before));
        var db = services.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.AccountRecoveryRequests.AnyAsync(
            request => (request.NewMethodValue == forAccount || request.NewMethodValue == forNobody)
                && !db.AccountRecoveryNotices.Any(notice => notice.Id == request.Id), Ct));
        // Una segunda pasada no repite el aviso.
        var afterFirstPass = await OutboxIdsAsync();
        await ActivatorUtilities.CreateInstance<AccountRecoveryNoticeWorker>(factory.Services).RunOnceAsync(Ct);
        Assert.Empty(await EmailsToAsync(services, account.Email.Value, afterFirstPass));
    }

    [Fact]
    public async Task A_failed_notice_commit_rolls_back_both_the_mark_and_outbox_and_allows_one_retry()
    {
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        using var anonymous = Client();
        var newEmail = $"aviso-rollback-{Guid.NewGuid():N}@example.test";
        await RecoveryJourney.SubmitAsync(factory, anonymous, newEmail, account.Email.Value, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await db.AccountRecoveryRequests.AsNoTracking().SingleAsync(item => item.NewMethodValue == newEmail, Ct);
        var before = await OutboxIdsAsync();
        var probe = new CommitFailureProbe
        {
            BeforeFailing = async (inside, ct) =>
            {
                Assert.True(await inside.AccountRecoveryNotices.AnyAsync(notice => notice.Id == request.Id, ct));
                Assert.True(await inside.OutboxMessages.AnyAsync(message => !before.Contains(message.Id)
                    && message.UserId == account.UserId, ct));
                Assert.Equal(request.Version, await inside.AccountRecoveryRequests.Where(item => item.Id == request.Id)
                    .Select(item => item.Version).SingleAsync(ct));
            },
        };
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => FailingCommitUnitOfWork.Replace(services, probe)));
        await using (var failingScope = failingFactory.Services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<ExpectedCommitFailure>(() => failingScope.ServiceProvider
                .GetRequiredService<IAccountRecoveryNoticeService>().NotifyAccountsAsync(Ct));
        }
        Assert.True(probe.RolledBackBeforeLeaving);
        Assert.False(await db.AccountRecoveryNotices.AnyAsync(notice => notice.Id == request.Id, Ct));
        Assert.Equal(before.Order(), (await OutboxIdsAsync()).Order());
        Assert.Equal(request.Version, await db.AccountRecoveryRequests.Where(item => item.Id == request.Id)
            .Select(item => item.Version).SingleAsync(Ct));

        await ActivatorUtilities.CreateInstance<AccountRecoveryNoticeWorker>(factory.Services).RunOnceAsync(Ct);
        Assert.True(await db.AccountRecoveryNotices.AnyAsync(notice => notice.Id == request.Id, Ct));
        Assert.Single(await EmailsToAsync(scope.ServiceProvider, account.Email.Value, before));
        var afterRetry = await OutboxIdsAsync();
        await ActivatorUtilities.CreateInstance<AccountRecoveryNoticeWorker>(factory.Services).RunOnceAsync(Ct);
        Assert.Equal(afterRetry.Order(), (await OutboxIdsAsync()).Order());
    }

    /// <summary>Pide, verifica y registra un pedido; cuenta los pedidos y los mensajes que escribió el registro.</summary>
    private async Task<(int Requests, int Messages)> SubmitCountingWritesAsync(HttpClient client, string newEmail,
        string previousEmail, bool accountExists = true)
    {
        var nonce = Nonce();
        using var requested = await AccountJourney.PostAsync(client, "/api/account-recovery/code",
            new { email = newEmail, nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, newEmail, Ct);
        using var verified = await AccountJourney.PostAsync(client, "/api/account-recovery/verify",
            new { email = newEmail, nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        var (requestsBefore, messagesBefore) = await CountWritesAsync();
        using var submitted = await AccountJourney.PostAsync(client, "/api/account-recovery",
            new { email = newEmail, nonce, previousEmail }, Ct);
        Assert.Equal(accountExists ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest, submitted.StatusCode);
        if (!accountExists) Assert.Equal(AccountRecoveryErrors.AccountNotFoundCode, await CodeAsync(submitted));
        var (requestsAfter, messagesAfter) = await CountWritesAsync();
        return (requestsAfter - requestsBefore, messagesAfter - messagesBefore);
    }

    private async Task<(int Requests, int Messages)> CountWritesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.AccountRecoveryRequests.CountAsync(Ct), await db.OutboxMessages.CountAsync(Ct));
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
