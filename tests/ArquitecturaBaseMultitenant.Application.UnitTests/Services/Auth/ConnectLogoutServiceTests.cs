using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>
/// Comprueba que salir revoque la autorización activa y sus tokens dentro de una sola transacción. Protege
/// el cierre de la sesión seleccionada.
/// </summary>
public sealed class ConnectLogoutServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revokes_tokens_and_updates_the_invitation_continuation(bool preserveInvitation)
    {
        using var fixture = new ServiceFixture<ConnectLogoutService>();
        var unitOfWork = new FakeUnitOfWork();
        var revoker = new StubTokenRevoker(unitOfWork);
        var invitationFlow = new StubInvitationFlowContext();
        var service = new ConnectLogoutService(revoker, unitOfWork, invitationFlow,
            fixture.TimeProvider, fixture.Logger);

        var userId = Guid.NewGuid();
        var result = await service.RevokeAsync(userId, "browser-session", "authorization-id",
            preserveInvitation, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("authorization-id", revoker.AuthorizationId);
        Assert.Equal(userId, revoker.UserId);
        Assert.Equal("browser-session", revoker.SessionId);
        Assert.True(revoker.WasInsideTransaction);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(preserveInvitation, invitationFlow.Preserved);
        Assert.Equal(!preserveInvitation, invitationFlow.Cleared);
    }

    private sealed class StubInvitationFlowContext : IInvitationFlowContext
    {
        public bool Preserved { get; private set; }
        public bool Cleared { get; private set; }
        public Task<Guid?> GetUserIdAsync(CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public InvitationContinuation? ReadContinuation() => null;
        public void Remember(InvitationContinuation continuation) { }
        public void PreserveContinuationForAccountSwitch() => Preserved = true;
        public void ClearContinuation() => Cleared = true;
        public Task<bool> RestoreSessionAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class StubTokenRevoker(FakeUnitOfWork unitOfWork) : ITokenRevoker
    {
        public string? AuthorizationId { get; private set; }
        public Guid? UserId { get; private set; }
        public string? SessionId { get; private set; }
        public bool WasInsideTransaction { get; private set; }

        public Task RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken)
        {
            AuthorizationId = authorizationId;
            WasInsideTransaction = unitOfWork.InTransaction;
            return Task.CompletedTask;
        }

        public Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeSessionAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
        {
            UserId = userId;
            SessionId = sessionId;
            WasInsideTransaction = unitOfWork.InTransaction;
            return Task.CompletedTask;
        }

        public Task RevokeAccessAsync(Guid userId, Access access, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
