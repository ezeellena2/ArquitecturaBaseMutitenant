using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class ConnectLogoutServiceTests
{
    [Fact]
    public async Task Revokes_the_active_authorization_and_tokens_in_one_transaction()
    {
        using var fixture = new ServiceFixture<ConnectLogoutService>();
        var unitOfWork = new FakeUnitOfWork();
        var revoker = new StubTokenRevoker(unitOfWork);
        var service = new ConnectLogoutService(revoker, unitOfWork, fixture.TimeProvider, fixture.Logger);

        var result = await service.RevokeAuthorizationAsync("authorization-id", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("authorization-id", revoker.AuthorizationId);
        Assert.True(revoker.WasInsideTransaction);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
    }

    private sealed class StubTokenRevoker(FakeUnitOfWork unitOfWork) : ITokenRevoker
    {
        public string? AuthorizationId { get; private set; }
        public bool WasInsideTransaction { get; private set; }

        public Task RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken)
        {
            AuthorizationId = authorizationId;
            WasInsideTransaction = unitOfWork.InTransaction;
            return Task.CompletedTask;
        }

        public Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeAccessAsync(Guid userId, Access access, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
