using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>
/// B4: el bloqueo por códigos fallidos no frena a Google, porque cualquiera que conozca el correo puede provocarlo;
/// la suspensión y la baja, sí.
/// </summary>
public sealed class GoogleAccountGuardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Google_does_not_read_the_lockout_of_failed_codes() =>
        Assert.DoesNotContain(typeof(GoogleAccountGuard).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ISignInService));

    [Theory]
    [InlineData(UserStatus.Active, null)]
    [InlineData(UserStatus.Suspended, AccountErrors.SuspendedCode)]
    [InlineData(UserStatus.Deleted, AccountErrors.SuspendedCode)]
    public async Task Suspension_and_deletion_still_stop_Google(UserStatus status, string? expectedError)
    {
        var store = new IdentityTestStore();
        var account = new UserAccountRow(Guid.CreateVersion7(), null, "es-AR", "America/Argentina/Buenos_Aires",
            status, IsPlatformOperator: false, PrimaryEmail: null, LastBusinessTenantId: null);
        store.Users.Add(account.Id, account);
        var guard = new GoogleAccountGuard(store, new AccountDeletionCancelIssuer(new NoTickets(), new NoSecrets(),
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero))));

        var checkedAccount = await guard.CheckAsync(account.Id,
            "/connect/authorize?client_id=web&access=consumer", Ct);

        if (expectedError is null) Assert.Equal(account.Id, checkedAccount.Value.Id);
        else Assert.Equal(expectedError, checkedAccount.Error.Code);
    }

    private sealed class NoTickets : IAccountDeletionCancelTicketRepository
    {
        public Task<Guid?> GetUserIdByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<AccountDeletionCancelTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Add(AccountDeletionCancelTicket ticket) => throw new NotSupportedException();
    }

    private sealed class NoSecrets : ISecureTokenGenerator
    {
        public string Generate() => throw new NotSupportedException();
        public string Hash(string token) => throw new NotSupportedException();
    }
}
