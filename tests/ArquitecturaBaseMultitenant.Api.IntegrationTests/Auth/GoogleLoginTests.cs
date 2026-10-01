using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba ingreso y vinculación de Google con persistencia. Protege la pertenencia del identificador
/// externo y evita emitir sesión durante una baja pendiente.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class GoogleLoginTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pending_Google_proof_returns_cancel_ticket_without_signing_in()
    {
        var subject = Guid.NewGuid().ToString("N");
        var email = Email.Create("google-pending-" + subject + "@example.test").Value;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var services = seedScope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                var user = await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
                var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                var method = LoginMethod.CreateGoogle(user.Id, subject, email);
                method.Verify(nowUtc);
                method.MakePrimary();
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                await users.RequestDeletionAsync(user.Id, "Prueba Google", nowUtc, 30, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject, email, true, null));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IExternalLoginService>()
            .SignInAsync(new ExternalSignInRequest("/connect/authorize?client_id=web&access=business", false, false), Ct);

        Assert.Equal(AccountErrors.PendingDeletion.Code, result.Error.Code);
        Assert.NotNull(result.Error.Metadata);
        Assert.True(result.Error.Metadata.ContainsKey("cancelTicket"));
        Assert.Null(google.SignedInUserId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Explicit_link_does_not_take_a_Google_subject_or_email_from_another_account(bool googleConflict)
    {
        var subject = Guid.NewGuid().ToString("N");
        var email = Email.Create("google-conflict-" + subject + "@example.test").Value;
        var userId = Guid.Empty;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var services = seedScope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                userId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                var other = await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
                var method = googleConflict ? LoginMethod.CreateGoogle(other.Id, subject, email)
                    : LoginMethod.CreateEmail(other.Id, email);
                method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject, email, true, null));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAccountGoogleService>()
            .LinkAsync(new LinkGoogleRequest(userId, userId), Ct);
        Assert.Equal(GoogleMethodErrors.AlreadyUsed.Code, result.Error.Code);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ILoginMethodReader>().ListByUserIdAsync(userId, Ct));
        Assert.Null(google.SignedInUserId);
    }

    [Fact]
    public async Task Explicit_link_reclaims_another_accounts_pending_email_under_the_destination_lock()
    {
        var subject = Guid.NewGuid().ToString("N");
        var email = Email.Create("google-pending-link-" + subject + "@example.test").Value;
        var userId = Guid.Empty;
        var pendingOwnerId = Guid.Empty;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var services = seedScope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                userId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                pendingOwnerId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                services.GetRequiredService<ILoginMethodRepository>()
                    .Add(LoginMethod.CreateEmail(pendingOwnerId, email));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject, email, true, null));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAccountGoogleService>()
            .LinkAsync(new LinkGoogleRequest(userId, userId), Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ILoginMethodReader>()
            .ListByUserIdAsync(pendingOwnerId, Ct));
        var linked = Assert.Single(await scope.ServiceProvider.GetRequiredService<ILoginMethodReader>()
            .ListByUserIdAsync(userId, Ct));
        Assert.Equal(LoginMethodType.Google, linked.Type);
        Assert.Equal(subject, linked.Value);
        Assert.Equal(email, linked.ContactEmail);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Explicit_link_requires_the_same_account_session_and_verified_Google_email(
        bool sameSession, bool emailVerified)
    {
        var userId = Guid.Empty;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var services = seedScope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                userId = (await services.GetRequiredService<IUserRepository>()
                    .CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var subject = Guid.NewGuid().ToString("N");
        var email = Email.Create("google-explicit-" + subject + "@example.test").Value;
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject, email, emailVerified, null));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAccountGoogleService>()
            .LinkAsync(new LinkGoogleRequest(userId, sameSession ? userId : Guid.CreateVersion7()), Ct);

        Assert.Equal(sameSession && emailVerified, result.IsSuccess);
        Assert.Null(google.SignedInUserId);
        Assert.Equal(sameSession && emailVerified ? 1 : 0, await CountMethodsAsync(subject));
        if (result.IsSuccess)
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var method = await context.LoginMethods.AsNoTracking().SingleAsync(row => row.UserId == userId, Ct);
            Assert.Equal(email, method.ContactEmail);
            Assert.True(method.IsPrimary);
            Assert.Equal(email.Value, (await context.Users.AsNoTracking().SingleAsync(row => row.Id == userId, Ct)).Email);
        }
    }

    [Fact]
    public async Task Login_door_does_not_create_an_account_for_an_unknown_Google_subject()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var subject = Guid.NewGuid().ToString("N");
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject,
            Email.Create($"google-{subject}@example.test").Value, true, "Persona nueva"));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExternalLoginService>()
            .SignInAsync(new ExternalSignInRequest("/connect/authorize?client_id=web", false, false), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.Google.AccountNotFound", result.Error.Code);
        Assert.Null(google.SignedInUserId);
        Assert.Equal(0, await CountMethodsAsync(subject));
    }

    [Fact]
    public async Task Registration_without_terms_is_rejected_before_creating_any_account()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var subject = Guid.NewGuid().ToString("N");
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject,
            Email.Create($"google-{subject}@example.test").Value, true, "Persona nueva"));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExternalLoginService>()
            .SignInAsync(new ExternalSignInRequest("/connect/authorize?client_id=web", true, false), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.Contains("acceptedTerms", Assert.IsType<ValidationError>(result.Error).Errors.Keys);
        Assert.Equal(0, await CountMethodsAsync(subject));
    }

    [Fact]
    public async Task Unverified_Google_email_cannot_create_or_link_an_account()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var subject = Guid.NewGuid().ToString("N");
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject,
            Email.Create($"google-{subject}@example.test").Value, false, "Persona nueva"));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExternalLoginService>()
            .SignInAsync(new ExternalSignInRequest("/connect/authorize?client_id=web", true, true), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.ExternalLogin.EmailNotVerified", result.Error.Code);
        Assert.Equal(0, await CountMethodsAsync(subject));
    }

    [Fact]
    public async Task Registration_creates_verified_Google_method_personal_space_and_both_legal_acceptances()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var subject = Guid.NewGuid().ToString("N");
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject,
            Email.Create($"google-{subject}@example.test").Value, true, "Persona nueva"));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExternalLoginService>()
            .SignInAsync(new ExternalSignInRequest("/connect/authorize?client_id=web", true, true,
                "es-AR", "America/Argentina/Buenos_Aires"), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(google.SignedInUserId);
        Assert.Equal(1, await CountMethodsAsync(subject));
        Assert.Equal(2, await CountAcceptancesAsync(google.SignedInUserId.Value));
        Assert.Equal(1, await CountPersonalSpacesAsync(google.SignedInUserId.Value));
    }

    [Fact]
    public async Task Verified_Google_email_links_an_existing_account_without_a_second_identity()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var address = $"google-link-{Guid.NewGuid():N}@example.test";
        var originalUserId = Guid.Empty;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var services = seedScope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                var methods = services.GetRequiredService<ILoginMethodRepository>();
                var user = await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
                originalUserId = user.Id;
                var email = Email.Create(address).Value;
                var method = ArquitecturaBaseMultitenant.Domain.Authentication.LoginMethod.CreateEmail(user.Id, email);
                method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                method.MakePrimary();
                methods.Add(method);
                await users.SetPrimaryEmailAsync(user.Id, email, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var subject = Guid.NewGuid().ToString("N");
        var google = new GoogleSignInDouble(new ExternalLogin("Google", subject,
            Email.Create(address).Value, true, "Nombre"));
        await using var host = HostWith(google);
        await using var scope = host.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExternalLoginService>()
            .SignInAsync(new ExternalSignInRequest("/connect/authorize?client_id=web", false, false), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await CountMethodsAsync(subject));
        Assert.Equal(originalUserId, google.SignedInUserId);
        Assert.Equal(1, await CountUsersByEmailAsync(address));
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWith(GoogleSignInDouble google) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISignInService>();
            services.AddSingleton<ISignInService>(google);
        }));

    private async Task<long> CountMethodsAsync(string subject) => await ScalarAsync(
        "SELECT count(*) FROM identity.\"LoginMethods\" WHERE \"Type\" = 'Google' AND \"Value\" = @value", subject);

    private async Task<long> CountUsersByEmailAsync(string address) => await ScalarAsync(
        "SELECT count(*) FROM identity.\"LoginMethods\" WHERE \"Type\" = 'Email' AND \"Value\" = @value", address);

    private async Task<long> ScalarAsync(string sql, string value)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("value", value);
        return (long)(await command.ExecuteScalarAsync(Ct) ?? 0L);
    }

    private async Task<long> CountAcceptancesAsync(Guid userId) => await CountByUserAsync(
        "SELECT count(*) FROM identity.\"LegalAcceptances\" WHERE \"UserId\" = @userId", userId);

    private async Task<long> CountPersonalSpacesAsync(Guid userId) => await CountByUserAsync(
        "SELECT count(*) FROM identity.\"UserTenantAccesses\" a JOIN platform.\"Tenants\" t ON t.\"Id\" = a.\"TenantId\" WHERE a.\"UserId\" = @userId AND t.\"Kind\" = 'Personal'", userId);

    private async Task<long> CountByUserAsync(string sql, Guid userId)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("userId", userId);
        return (long)(await command.ExecuteScalarAsync(Ct) ?? 0L);
    }

    private sealed class GoogleSignInDouble(ExternalLogin login) : ISignInService
    {
        public Guid? SignedInUserId { get; private set; }

        public Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ExternalLogin?>(login);

        public Task SignOutExternalAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task RegisterFailedAttemptAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SignInAsync(Guid userId, CancellationToken cancellationToken)
        {
            SignedInUserId = userId;
            return Task.CompletedTask;
        }
    }
}
