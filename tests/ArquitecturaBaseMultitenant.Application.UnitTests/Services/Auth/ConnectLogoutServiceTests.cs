using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
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
        var sessions = new StubPlatformSessions(unitOfWork);
        var service = new ConnectLogoutService(revoker, sessions, new HashingTokens(), unitOfWork, invitationFlow,
            fixture.TimeProvider, fixture.Logger);

        var userId = Guid.NewGuid();
        var result = await service.RevokeAsync(userId, "browser-session", "authorization-id",
            preserveInvitation, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("authorization-id", revoker.AuthorizationId);
        Assert.Equal(userId, revoker.UserId);
        Assert.Equal("browser-session", revoker.SessionId);
        // E9: salir también quita la evidencia del segundo factor de ese navegador.
        Assert.Equal((userId, "hash:browser-session"), sessions.Revoked);
        Assert.True(sessions.WasInsideTransaction);
        Assert.True(revoker.WasInsideTransaction);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(preserveInvitation, invitationFlow.Preserved);
        Assert.Equal(!preserveInvitation, invitationFlow.Cleared);
    }

    private sealed class HashingTokens : ISecureTokenGenerator
    {
        public string Generate() => throw new NotSupportedException();
        public string Hash(string token) => "hash:" + token;
    }

    private sealed class StubPlatformSessions(FakeUnitOfWork unitOfWork) : IPlatformFactorRepository
    {
        public (Guid UserId, string Hash)? Revoked { get; private set; }
        public bool WasInsideTransaction { get; private set; }

        public Task RevokeSessionAsync(Guid userId, string sessionIdHash, DateTime nowUtc, CancellationToken cancellationToken)
        {
            Revoked = (userId, sessionIdHash);
            WasInsideTransaction = unitOfWork.InTransaction;
            return Task.CompletedTask;
        }

        public Task LockAccountAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformAuthenticator?> GetAuthenticatorAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(PlatformAuthenticator authenticator) => throw new NotSupportedException();
        public Task<PlatformRecoveryCode?> GetRecoveryCodeAsync(Guid userId, string codeHash, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(IEnumerable<PlatformRecoveryCode> codes) => throw new NotSupportedException();
        public Task<PlatformFactorChallenge?> GetChallengeForUpdateAsync(Guid challengeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(PlatformFactorChallenge challenge) => throw new NotSupportedException();
        public Task<PlatformSession?> GetSessionAsync(Guid platformSessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(PlatformSession session) => throw new NotSupportedException();
        public Task<PlatformFactorOperation?> GetOperationAsync(Guid idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(PlatformFactorOperation operation) => throw new NotSupportedException();
        public Task InvalidateChallengesAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RevokeSessionsAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RemoveFactorAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
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
