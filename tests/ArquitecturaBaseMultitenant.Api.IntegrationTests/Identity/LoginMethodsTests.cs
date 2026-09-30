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

[Collection(ApiTestGroup.Name)]
public sealed class LoginMethodsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
