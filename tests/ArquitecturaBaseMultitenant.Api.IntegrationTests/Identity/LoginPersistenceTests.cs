using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba la persistencia de métodos, códigos, auditoría y preferencias de cuenta. Exige transacción
/// para escribir y unicidad global del identificador de ingreso.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LoginPersistenceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_code_can_be_consumed_only_once_under_destination_lock()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<ILoginCodeRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var destination = LoginCodeDestination.ForEmail(Email.Create($"code-{Guid.NewGuid():N}@example.test").Value);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var code = LoginCode.Issue(destination, LoginCodePurpose.Login, null,
            "hash", nowUtc, TimeSpan.FromMinutes(10), maxAttempts: 3);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await repository.LockDestinationAsync(destination, ct);
            repository.Add(code);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var first = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await repository.LockDestinationAsync(destination, ct);
            var latest = await repository.GetLatestAsync(destination, LoginCodePurpose.Login, null, ct);
            return latest!.Verify(LoginCodePurpose.Login, "hash", nowUtc);
        }, CommitPolicy.OnAnyResult, Ct);

        var second = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await repository.LockDestinationAsync(destination, ct);
            var latest = await repository.GetLatestAsync(destination, LoginCodePurpose.Login, null, ct);
            return latest!.Verify(LoginCodePurpose.Login, "hash", nowUtc);
        }, CommitPolicy.OnAnyResult, Ct);

        Assert.True(first.IsSuccess);
        Assert.Equal(LoginCodeErrors.AlreadyUsed.Code, second.Error.Code);
    }

    [Fact]
    public async Task User_method_and_audit_repositories_require_transaction()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var methods = services.GetRequiredService<ILoginMethodRepository>();
        var audits = services.GetRequiredService<ILoginAuditRepository>();
        var email = Email.Create($"method-{Guid.NewGuid():N}@example.test").Value;
        var method = LoginMethod.CreateEmail(Guid.CreateVersion7(), email);
        var audit = LoginAudit.Success(Guid.CreateVersion7(), LoginAuditMethod.Code,
            services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);

        Assert.Throws<InvalidOperationException>(() => methods.Add(method));
        Assert.Throws<InvalidOperationException>(() => audits.Add(audit));
    }

    // D4b (ADR 0041): un método verificado es único en todo el sistema; uno pendiente, solo dentro de su cuenta, así
    // el correo de otra cuenta queda pendiente como uno libre.
    [Fact]
    public async Task A_verified_method_value_is_unique_across_accounts_and_a_pending_one_within_its_account()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<IUserRepository>();
        var methods = services.GetRequiredService<ILoginMethodRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var email = Email.Create($"unique-{Guid.NewGuid():N}@example.test").Value;
        Guid firstId = Guid.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            firstId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var verified = LoginMethod.CreateEmail(firstId, email);
            verified.Verify(nowUtc);
            methods.Add(verified);
            var second = await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
            methods.Add(LoginMethod.CreateEmail(second.Id, email));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var third = await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
                var duplicate = LoginMethod.CreateEmail(third.Id, email);
                duplicate.Verify(nowUtc);
                methods.Add(duplicate);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct));

        await using var pendingScope = factory.Services.CreateAsyncScope();
        var pendingServices = pendingScope.ServiceProvider;
        var pendingMethods = pendingServices.GetRequiredService<ILoginMethodRepository>();
        var pendingUsers = pendingServices.GetRequiredService<IUserRepository>();
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() =>
            pendingServices.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var fourth = await pendingUsers.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
                pendingMethods.Add(LoginMethod.CreateEmail(fourth.Id, email));
                pendingMethods.Add(LoginMethod.CreateEmail(fourth.Id, email));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct));
    }

    [Fact]
    public async Task User_preferences_primary_email_and_last_business_are_persisted_together()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<IUserRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var email = Email.Create($"profile-{Guid.NewGuid():N}@example.test").Value;
        var tenantId = Guid.NewGuid();
        Guid userId = Guid.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            userId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            await users.SetPrimaryEmailAsync(userId, email, ct);
            var version = (await users.GetByIdAsync(userId, ct))!.Version;
            await users.UpdateProfileAsync(userId, "Ana", "en-US", "America/New_York", version, ct);
            await users.RememberBusinessTenantAsync(userId, tenantId, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var account = await users.GetByIdAsync(userId, Ct);
        Assert.NotNull(account);
        Assert.Equal("Ana", account.DisplayName);
        Assert.Equal("en-US", account.Culture);
        Assert.Equal("America/New_York", account.TimeZoneId);
        Assert.Equal(email, account.PrimaryEmail);
        Assert.Equal(tenantId, account.LastBusinessTenantId);
    }
}
