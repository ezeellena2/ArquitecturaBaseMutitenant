using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removing_the_primary_moves_its_copy_atomically_and_expired_tickets_change_nothing(bool expired)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var secrets = services.GetRequiredService<ISecureTokenGenerator>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var first = LoginMethod.CreateEmail(user.Id, Email.Create(user.Id.ToString("N") + "@example.test").Value);
        var backup = LoginMethod.CreateEmail(user.Id, Email.Create("backup-" + user.Id.ToString("N") + "@example.test").Value);
        first.Verify(nowUtc);
        first.MakePrimary();
        backup.Verify(nowUtc);
        var secret = secrets.Generate();
        var ticket = ReauthTicket.Issue(user.Id, ReauthAction.RemoveMethod, backup.Id, first.Id,
            secrets.Hash(secret), expired ? nowUtc.AddMinutes(-6) : nowUtc).Value;
        await work.ExecuteInTransactionAsync(async ct =>
        {
            Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user)).Succeeded);
            context.LoginMethods.AddRange(first, backup);
            context.ReauthTickets.Add(ticket);
            await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(user.Id, Email.Create(first.Value).Value, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var service = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services, new TestCurrentUser(user.Id));
        var result = await service.RemoveAsync(new ChangeLoginMethodRequest(first.Id, secret), Ct);
        Assert.Equal(!expired, result.IsSuccess);
        if (expired) Assert.Equal(ReauthErrors.Expired.Code, result.Error.Code);
        context.ChangeTracker.Clear();
        var methods = await context.LoginMethods.AsNoTracking().Where(row => row.UserId == user.Id).ToArrayAsync(Ct);
        Assert.Equal(expired ? 2 : 1, methods.Length);
        Assert.Equal(expired ? first.Id : backup.Id, methods.Single(row => row.IsPrimary).Id);
        Assert.Equal(expired ? first.Value : backup.Value,
            (await context.Users.AsNoTracking().SingleAsync(row => row.Id == user.Id, Ct)).Email);
    }

    [Fact]
    public async Task Primary_and_removal_require_another_method_and_a_one_use_context_bound_ticket()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var first = LoginMethod.CreateEmail(user.Id, Email.Create(user.Id.ToString("N") + "@example.test").Value);
        var second = LoginMethod.CreateEmail(user.Id, Email.Create("second-" + user.Id.ToString("N") + "@example.test").Value);
        first.Verify(nowUtc);
        first.MakePrimary();
        second.Verify(nowUtc);
        await work.ExecuteInTransactionAsync(async ct =>
        {
            Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user)).Succeeded);
            context.LoginMethods.AddRange(first, second);
            await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(user.Id, Email.Create(first.Value).Value, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var current = new TestCurrentUser(user.Id);
        var reauth = ActivatorUtilities.CreateInstance<ReauthService>(services, current);
        var management = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services, current);

        var challenge = await reauth.RequestAsync(new RequestReauthRequest(ReauthAction.MakePrimary, second.Id), Ct);
        Assert.True(challenge.IsSuccess);
        Assert.Equal(first.Id, challenge.Value.SourceMethodId);
        var code = await PickupCodeReader.ReadAsync(services, first.Value, Ct);
        Assert.Equal(ReauthErrors.Invalid.Code, (await reauth.VerifyAsync(new VerifyReauthRequest(
            ReauthAction.RemoveMethod, second.Id, first.Id, code), Ct)).Error.Code);
        var verified = await reauth.VerifyAsync(new VerifyReauthRequest(ReauthAction.MakePrimary, second.Id, first.Id, code), Ct);
        Assert.True(verified.IsSuccess);
        var primary = new ChangeLoginMethodRequest(second.Id, verified.Value.ReauthTicket);
        Assert.True((await management.MakePrimaryAsync(primary, Ct)).IsSuccess);
        Assert.True((await management.MakePrimaryAsync(primary, Ct)).IsFailure);

        challenge = await reauth.RequestAsync(new RequestReauthRequest(ReauthAction.RemoveMethod, first.Id), Ct);
        Assert.Equal(second.Id, challenge.Value.SourceMethodId);
        code = await PickupCodeReader.ReadAsync(services, second.Value, Ct);
        verified = await reauth.VerifyAsync(new VerifyReauthRequest(ReauthAction.RemoveMethod, first.Id, second.Id, code), Ct);
        Assert.True((await management.RemoveAsync(new ChangeLoginMethodRequest(first.Id, verified.Value.ReauthTicket), Ct)).IsSuccess);
        context.ChangeTracker.Clear();
        var remaining = await context.LoginMethods.AsNoTracking().SingleAsync(row => row.UserId == user.Id, Ct);
        Assert.Equal(second.Id, remaining.Id);
        Assert.True(remaining.IsPrimary);
        Assert.Equal(second.Value, (await context.Users.AsNoTracking().SingleAsync(row => row.Id == user.Id, Ct)).Email);
        Assert.Equal(LoginMethodErrors.LastMethod.Code,
            (await reauth.RequestAsync(new RequestReauthRequest(ReauthAction.RemoveMethod, second.Id), Ct)).Error.Code);
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
            new TestCurrentUser(user.Id));
        var email = Email.Create("added-" + user.Id.ToString("N") + "@example.test").Value;
        var lookup = services.GetRequiredService<IUserLookup>();

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
    public async Task Another_accounts_pending_email_is_reserved_and_its_method_cannot_be_verified()
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
            new TestCurrentUser(other.Id));

        Assert.Equal(LoginMethodErrors.AlreadyUsedCode,
            (await service.AddEmailAsync(new AddLoginEmailRequest(email), Ct)).Error.Code);
        Assert.Equal(LoginMethodErrors.NotFound.Code,
            (await service.VerifyAsync(new VerifyLoginMethodRequest(method.Id, "123456"), Ct)).Error.Code);
    }

    private sealed record TestCurrentUser(Guid Id) : ICurrentUser
    {
        public Guid? UserId => Id;
        public Access? Access => Domain.Users.Access.Consumer;
    }

    [Fact]
    public async Task Provider_contact_and_one_use_ticket_round_trip_in_the_runtime_database()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var methods = services.GetRequiredService<ILoginMethodRepository>();
        var tickets = services.GetRequiredService<IReauthTicketRepository>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var google = LoginMethod.CreateGoogle(user.Id, Guid.NewGuid().ToString("N"),
            Email.Create("contact-" + Guid.NewGuid().ToString("N") + "@example.test").Value);
        google.Verify(nowUtc);
        google.MakePrimary();
        var tokenHash = Guid.NewGuid().ToString("N");
        var ticket = ReauthTicket.Issue(user.Id, ReauthAction.DeleteAccount, google.Id, null,
            tokenHash, nowUtc).Value;
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
            Assert.True(saved.Consume(user.Id, ReauthAction.DeleteAccount, null, nowUtc).IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        context.ChangeTracker.Clear();
        Assert.NotNull((await context.ReauthTickets.AsNoTracking().SingleAsync(row => row.Id == ticket.Id, Ct)).ConsumedAtUtc);
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
