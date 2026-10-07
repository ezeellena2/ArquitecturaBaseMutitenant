using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba las reglas persistidas al agregar, verificar, quitar y elegir métodos. Exige pruebas de
/// titularidad válidas y cambios atómicos del método principal.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LoginMethodsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Removing_the_primary_moves_its_copy_atomically_without_a_confirmation_code()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var first = LoginMethod.CreateEmail(user.Id, Email.Create(user.Id.ToString("N") + "@example.test").Value);
        var backup = LoginMethod.CreateEmail(user.Id, Email.Create("backup-" + user.Id.ToString("N") + "@example.test").Value);
        first.Verify(nowUtc);
        first.MakePrimary();
        backup.Verify(nowUtc);
        await work.ExecuteInTransactionAsync(async ct =>
        {
            Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user)).Succeeded);
            context.LoginMethods.AddRange(first, backup);
            await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(user.Id, Email.Create(first.Value).Value, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var service = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services, new TestCurrentUser(user.Id, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
        var result = await service.RemoveAsync(new ChangeLoginMethodRequest(first.Id), Ct);
        Assert.True(result.IsSuccess);
        context.ChangeTracker.Clear();
        var methods = await context.LoginMethods.AsNoTracking().Where(row => row.UserId == user.Id).ToArrayAsync(Ct);
        Assert.Single(methods);
        Assert.Equal(backup.Id, methods.Single(row => row.IsPrimary).Id);
        Assert.Equal(backup.Value,
            (await context.Users.AsNoTracking().SingleAsync(row => row.Id == user.Id, Ct)).Email);
    }

    [Fact]
    public async Task Added_email_cannot_sign_in_until_its_own_verification_code_is_consumed()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        await work.ExecuteInTransactionAsync(async ct =>
        {
            Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user)).Succeeded);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var service = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services,
            new TestCurrentUser(user.Id, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
        var email = Email.Create("added-" + user.Id.ToString("N") + "@example.test").Value;
        var lookup = services.GetRequiredService<IUserLookup>();

        await AddSourceMethodAsync(services, user.Id);
        var added = await service.AddEmailAsync(new AddLoginEmailRequest(email), Ct);
        Assert.True(added.IsSuccess);
        Assert.Null(await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, email.Value, Ct));
        var code = await PickupCodeReader.ReadAsync(services, email.Value, Ct);
        var loginCode = code == "123456" ? "654321" : "123456";
        var destination = LoginCodeDestination.ForEmail(email);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.LoginCodes.Add(LoginCode.Issue(destination, LoginCodePurpose.Login, null,
                services.GetRequiredService<ILoginCodeHasher>().Hash(destination, LoginCodePurpose.Login, loginCode),
                nowUtc, TimeSpan.FromMinutes(10), 5));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True((await service.VerifyAsync(new VerifyLoginMethodRequest(added.Value.MethodId, loginCode), Ct)).IsFailure);
        context.ChangeTracker.Clear();
        Assert.Equal(1, (await context.LoginCodes.AsNoTracking().SingleAsync(row =>
            row.RequestedByUserId == user.Id && row.Purpose == LoginCodePurpose.VerifyDestination, Ct)).FailedAttempts);
        var verified = await service.VerifyAsync(new VerifyLoginMethodRequest(added.Value.MethodId, code), Ct);
        Assert.True(verified.IsSuccess);
        Assert.Equal(user.Id, await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, email.Value, Ct));
        Assert.True((await service.VerifyAsync(new VerifyLoginMethodRequest(added.Value.MethodId, code), Ct)).IsFailure);
    }

    [Fact]
    public async Task Adding_an_email_reclaims_another_accounts_pending_method_under_the_destination_lock()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var owner = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var other = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var email = Email.Create(owner.Id.ToString("N") + "@example.test").Value;
        var method = LoginMethod.CreateEmail(owner.Id, email);
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.Users.AddRange(owner, other);
            context.LoginMethods.Add(method);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var service = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services,
            new TestCurrentUser(other.Id, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));

        await AddSourceMethodAsync(services, other.Id);
        var added = await service.AddEmailAsync(new AddLoginEmailRequest(email), Ct);
        Assert.True(added.IsSuccess);
        var methods = services.GetRequiredService<ILoginMethodReader>();
        Assert.Empty(await methods.ListByUserIdAsync(owner.Id, Ct));
        var pendingForOther = Assert.Single(await methods.ListByUserIdAsync(other.Id, Ct),
            value => value.VerifiedAtUtc is null);
        Assert.Equal(added.Value.MethodId, pendingForOther.Id);
        Assert.Null(pendingForOther.VerifiedAtUtc);
        Assert.Equal(LoginMethodErrors.NotFound.Code,
            (await service.VerifyAsync(new VerifyLoginMethodRequest(method.Id, "123456"), Ct)).Error.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_expired_verification_code_expires_its_pending_method_instead_of_resending_or_verifying_it(
        bool resend)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var email = Email.Create("expired-pending-" + user.Id.ToString("N") + "@example.test").Value;
        var method = LoginMethod.CreateEmail(user.Id, email);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var destination = LoginCodeDestination.ForEmail(email);
        var expiredCode = LoginCode.Issue(destination, LoginCodePurpose.VerifyDestination, user.Id,
            services.GetRequiredService<ILoginCodeHasher>().Hash(destination, LoginCodePurpose.VerifyDestination, "123456"),
            nowUtc.AddMinutes(-11), TimeSpan.FromMinutes(10), 5);
        await work.ExecuteInTransactionAsync(async ct =>
        {
            Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user)).Succeeded);
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            context.LoginCodes.Add(expiredCode);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var service = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services,
            new TestCurrentUser(user.Id, nowUtc));
        var error = resend
            ? (await service.SendCodeAsync(method.Id, Ct)).Error
            : (await service.VerifyAsync(new VerifyLoginMethodRequest(method.Id, "123456"), Ct)).Error;

        Assert.Equal(LoginCodeErrors.Expired.Code, error.Code);
        Assert.Empty(await services.GetRequiredService<ILoginMethodReader>().ListByUserIdAsync(user.Id, Ct));
    }

    private static async Task AddSourceMethodAsync(IServiceProvider services, Guid userId)
    {
        var method = LoginMethod.CreateEmail(userId, Email.Create("source-" + userId.ToString("N") + "@example.test").Value);
        method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddSeconds(-1));
        method.MakePrimary();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private sealed record TestCurrentUser(Guid Id, DateTime StartedAtUtc) : ICurrentUser
    {
        public Guid? UserId => Id;
        public DateTime? SessionStartedAtUtc => StartedAtUtc;
        public Access? Access => Domain.Users.Access.Consumer;
    }

    [Fact]
    public async Task Provider_contact_and_cancellation_proof_round_trip_in_the_runtime_database()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var methods = services.GetRequiredService<ILoginMethodRepository>();
        var tickets = services.GetRequiredService<IAccountDeletionCancelTicketRepository>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var google = LoginMethod.CreateGoogle(user.Id, Guid.NewGuid().ToString("N"),
            Email.Create("contact-" + Guid.NewGuid().ToString("N") + "@example.test").Value);
        google.Verify(nowUtc);
        google.MakePrimary();
        var tokenHash = Guid.NewGuid().ToString("N");
        var ticket = AccountDeletionCancelTicket.Issue(user.Id, tokenHash, nowUtc, "/connect/authorize?access=consumer");
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.Users.Add(user);
            methods.Add(google);
            tickets.Add(ticket);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        context.ChangeTracker.Clear();

        await work.ExecuteInTransactionAsync(async ct =>
        {
            await methods.LockUserAsync(user.Id, ct);
            var loaded = await methods.GetByIdForUserAsync(user.Id, google.Id, ct);
            Assert.Equal(google.ContactEmail, loaded!.ContactEmail);
            var saved = await tickets.GetByHashAsync(tokenHash, ct);
            Assert.NotNull(saved);
            Assert.True(saved.Consume(user.Id, nowUtc).IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        context.ChangeTracker.Clear();
        Assert.NotNull((await context.AccountDeletionCancelTickets.AsNoTracking().SingleAsync(row => row.Id == ticket.Id, Ct)).ConsumedAtUtc);
    }

    [Fact]
    public async Task Database_rejects_two_primary_methods_for_the_same_account()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.Users.Add(user);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => work.ExecuteInTransactionAsync(ct =>
        {
            foreach (var suffix in new[] { "a", "b" })
            {
                var method = LoginMethod.CreateEmail(user.Id,
                    Email.Create(user.Id.ToString("N") + suffix + "@example.test").Value);
                method.Verify(nowUtc);
                method.MakePrimary();
                context.LoginMethods.Add(method);
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct));
    }

    [Fact]
    public async Task Owned_method_lookup_cannot_read_another_identitys_method()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var methods = services.GetRequiredService<ILoginMethodRepository>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var method = LoginMethod.CreateEmail(user.Id,
            Email.Create(user.Id.ToString("N") + "@example.test").Value);
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.Users.Add(user);
            methods.Add(method);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        await work.ExecuteInTransactionAsync(async ct =>
        {
            Assert.Null(await methods.GetByIdForUserAsync(Guid.CreateVersion7(), method.Id, ct));
            Assert.Empty(await methods.ListByUserIdAsync(Guid.CreateVersion7(), ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }
}
