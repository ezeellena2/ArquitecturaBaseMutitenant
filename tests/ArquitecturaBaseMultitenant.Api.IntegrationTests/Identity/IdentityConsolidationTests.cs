using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class IdentityConsolidationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("account")]
    [InlineData("operator")]
    [InlineData("recovery")]
    public async Task Google_and_email_writers_share_the_identity_lock_and_recheck_the_owner(string route)
    {
        var destination = Email.Create($"race-{Guid.NewGuid():N}@example.test").Value;
        var gate = new IdentityGate(destination);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var registration = Assert.Single(services, item => item.ServiceType == typeof(IExternalLoginLock));
            services.Remove(registration);
            services.AddScoped<IExternalLoginLock>(provider => new PausedIdentityLock(
                (IExternalLoginLock)ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!), gate));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        Guid recoveryId = default;
        uint recoveryVersion = default;
        if (route != "account")
        {
            using var person = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            if (route == "recovery")
            {
                var previous = await AccountJourney.RegisterAsync(host, person, Ct);
                await RecoveryJourney.SubmitAsync(factory, person, destination.Value, previous.Email.Value, Ct);
                await using var read = host.Services.CreateAsyncScope();
                var recovery = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests
                    .AsNoTracking().SingleAsync(row => row.NewMethodValue == destination.Value, Ct);
                recoveryId = recovery.Id;
                recoveryVersion = recovery.Version;
            }
            await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
            await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        }

        var google = RegisterGoogleAsync(host.Services, destination);
        await gate.GoogleLocked.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        Task<HttpResponseMessage> email = route switch
        {
            "account" => AccountJourney.PostAsync(client, "/api/me/login-methods", new { email = destination.Value }, Ct),
            "operator" => PlatformJourney.PostAsync(client, "/api/platform/operators/invitations",
                new { email = destination.Value, permissions = new[] { PlatformPermissions.Tenants.Read } }, Ct),
            _ => PlatformJourney.PostAsync(client, $"/api/platform/recoveries/{recoveryId:D}/approve",
                new { version = recoveryVersion, reason = "Revisión de prueba" }, Ct),
        };
        try
        {
            await gate.EmailStarted.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
            await Assert.ThrowsAsync<TimeoutException>(() => email.WaitAsync(TimeSpan.FromMilliseconds(300), Ct));
        }
        finally { gate.ReleaseGoogle.TrySetResult(); }
        var googleId = await google;
        using var response = await email;
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(route == "account" ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(route switch
        {
            "account" => LoginMethodErrors.AlreadyUsed.Code,
            "operator" => "Platform.Operator.AccountExists",
            _ => "Platform.AccountRecovery.MethodInUse",
        }, problem.GetProperty("code").GetString());
        await using var check = host.Services.CreateAsyncScope();
        var methods = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.AsNoTracking()
            .Where(method => method.Value == destination.Value || method.ContactEmail == destination).ToArrayAsync(Ct);
        Assert.Single(methods);
        Assert.Equal(googleId, methods[0].UserId);
        Assert.Equal(LoginMethodType.Google, methods[0].Type);
    }

    [Fact]
    public async Task Failed_signup_transaction_rolls_back_identity_method_personal_space_and_consent_together()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        var email = Email.Create($"rollback-{Guid.NewGuid():N}@example.test").Value;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var draft = await services.GetRequiredService<IPersonalSpaceProvisioner>().PrepareAsync(null, null, Ct);
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(draft.Tenant.Id);
        Guid createdId = default;
        var failed = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var registrar = services.GetRequiredService<SignupAccountRegistrar>();
            var documents = await registrar.GetCurrentLegalAsync(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, ct);
            await services.GetRequiredService<LoginMethodLinker>().LockEmailAsync(email, ct);
            createdId = await registrar.RegisterAsync(email, draft, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
                documents.Terms, documents.Privacy, ct);
            return Result.Failure(UserErrors.InvalidTransition);
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(failed.IsFailure);
        await using var read = factory.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Users.AnyAsync(row => row.Id == createdId, Ct));
        Assert.False(await db.LoginMethods.AnyAsync(row => row.UserId == createdId, Ct));
        Assert.False(await db.LegalAcceptances.AnyAsync(row => row.UserId == createdId, Ct));
        Assert.False(await db.Tenants.AnyAsync(row => row.Id == draft.Tenant.Id, Ct));
    }

    private static async Task<Guid> RegisterGoogleAsync(IServiceProvider provider, Email email)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var resolver = services.GetRequiredService<GoogleAccountResolver>();
        var draft = await resolver.PrepareAsync(null, null, Ct);
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(draft.Tenant.Id);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            resolver.ResolveAsync(new ExternalSignInRequest("/connect/authorize?client_id=web", true, true),
                new ExternalLogin("Google", Guid.NewGuid().ToString("N"), email, true, null), draft, ct), CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
        return result.Value;
    }

    private sealed class IdentityGate(Email destination)
    {
        internal Email Destination { get; } = destination;
        internal TaskCompletionSource GoogleLocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource EmailStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseGoogle { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PausedIdentityLock(IExternalLoginLock inner, IdentityGate gate) : IExternalLoginLock
    {
        public Task LockEmailAsync(Email email, CancellationToken ct)
        {
            if (email == gate.Destination && gate.GoogleLocked.Task.IsCompleted) gate.EmailStarted.TrySetResult();
            return inner.LockEmailAsync(email, ct);
        }
        public async Task AcquireAsync(string providerSubject, Email? email, CancellationToken ct)
        {
            await inner.AcquireAsync(providerSubject, email, ct);
            if (email != gate.Destination) return;
            gate.GoogleLocked.TrySetResult();
            await gate.ReleaseGoogle.Task.WaitAsync(ct);
        }
    }
}
