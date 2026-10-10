using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class EmailAccountStatesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Personal_signup_rejects_existing_accounts_including_operators_without_a_code_or_notice(bool platform)
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        if (platform) await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        await AgeRegistrationAsync(account.Email.Value);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var before = await CountsAsync(account.Email.Value);
        var key = Guid.NewGuid().ToString("D");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/signup")
                { Content = JsonContent.Create(new { email = account.Email.Value.ToUpperInvariant(), acceptedTerms = true }) };
            request.Headers.Add("Idempotency-Key", key);
            using var response = await anonymous.SendAsync(request, Ct);
            await AssertFieldAsync(response, HttpStatusCode.BadRequest, "Auth.Signup.EmailTaken", "email", "Ya tenés una cuenta con ese correo.");
            Assert.DoesNotContain("Platform", await response.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
            if (attempt > 0) Assert.True(response.Headers.Contains("Idempotent-Replayed"));
        }
        Assert.Equal(before, await CountsAsync(account.Email.Value));
    }

    [Fact]
    public async Task Recovery_preview_rejects_unknown_previous_email_and_does_not_issue_or_send()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var email = $"unknown-{Guid.NewGuid():N}@example.test";
        var before = await CountsAsync(email);
        using var response = await client.PostAsJsonAsync("/api/account-recovery/preview", new { previousEmail = email }, Ct);
        await AssertFieldAsync(response, HttpStatusCode.BadRequest, "Platform.AccountRecovery.AccountNotFound", "previousEmail", "No hay una cuenta con ese correo.");
        Assert.Equal(before, await CountsAsync(email));
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var knownBefore = await CountsAsync(account.Email.Value);
        using var known = await client.PostAsJsonAsync("/api/account-recovery/preview", new { previousEmail = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, known.StatusCode);
        Assert.False(known.Headers.Contains("Set-Cookie"));
        Assert.Equal(knownBefore, await CountsAsync(account.Email.Value));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recovery_and_add_email_reject_verified_ownership_without_codes_pending_methods_or_notices(bool recovery)
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        using var accountClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, accountClient, Ct);
        await AgeRegistrationAsync(owner.Email.Value);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var before = await CountsAsync(owner.Email.Value);
        using var response = recovery
            ? await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code", new { email = owner.Email.Value, nonce = new string('A', 43) }, Ct)
            : await AccountJourney.PostAsync(accountClient, "/api/me/login-methods", new { email = owner.Email.Value }, Ct);
        await AssertFieldAsync(response, HttpStatusCode.BadRequest,
            recovery ? "Platform.AccountRecovery.MethodInUse" : "Identity.LoginMethod.AlreadyUsed", "email",
            recovery ? "Ese método de ingreso ya es de otra cuenta." : "Ese correo ya está asociado a otra cuenta.");
        Assert.Equal(before, await CountsAsync(owner.Email.Value));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.AnyAsync(
            method => method.UserId == account.UserId && method.Value == owner.Email.Value, Ct));
    }

    private async Task AgeRegistrationAsync(string email)
    {
        // Como AccountJourney: el código consumido sólo prepara la cuenta, antes de la ventana del caso.
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var codes = await db.LoginCodes.Where(code => code.Destination == email && code.ConsumedAtUtc != null).ToListAsync(ct);
            var setupAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1);
            foreach (var code in codes) db.Entry(code).Property(item => item.CreatedAtUtc).CurrentValue = setupAtUtc;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    [Fact]
    public async Task Submit_rechecks_new_email_ownership_after_its_code_was_verified()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var email = $"race-{Guid.NewGuid():N}@example.test";
        var nonce = new string('B', 43);
        using var requested = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code", new { email, nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var verified = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify", new { email, nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var google = LoginMethod.CreateGoogle(owner.UserId, "google-" + Guid.NewGuid().ToString("N"), Email.Create(email).Value);
                Assert.True(google.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                services.GetRequiredService<ILoginMethodRepository>().Add(google);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var before = await CountsAsync(email);
        using var submitted = await AccountJourney.PostAsync(anonymous, "/api/account-recovery", new { email, nonce, previousEmail = owner.Email.Value }, Ct);
        await AssertFieldAsync(submitted, HttpStatusCode.BadRequest, "Platform.AccountRecovery.MethodInUse", "email", "Ese método de ingreso ya es de otra cuenta.");
        Assert.Equal(before, await CountsAsync(email));
        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests.AnyAsync(request => request.NewMethodValue == email, Ct));
    }

    private async Task<(int Codes, int Messages, int Recoveries)> CountsAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.LoginCodes.CountAsync(code => code.Destination == email, Ct),
            await db.OutboxMessages.CountAsync(Ct), await db.AccountRecoveryRequests.CountAsync(Ct));
    }

    private static async Task AssertFieldAsync(HttpResponseMessage response, HttpStatusCode status, string code, string field, string text)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(text, body.GetProperty("errors").GetProperty(field)[0].GetString());
        Assert.True(body.TryGetProperty("traceId", out _));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }
}
