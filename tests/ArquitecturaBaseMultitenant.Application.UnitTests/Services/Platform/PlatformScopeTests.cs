using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Platform;

public sealed class PlatformScopeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Permission_failure_neither_records_nor_enters_a_scope()
    {
        var boundary = new Boundary();
        var events = new Events();
        var scope = new Scope(boundary);
        var service = new PlatformScopeAccessService(new Current(Access.Platform), new Permissions(false),
            events, boundary, TimeProvider.System, new Request(), NullLogger<PlatformScopeAccessService>.Instance);
        var guard = new PlatformActionGuard(service, scope);
        var result = await guard.EnterAsync(Guid.NewGuid(), "Revisar", Ct);
        Assert.True(result.IsFailure);
        Assert.Empty(events.Items);
        Assert.Equal(0, scope.Entries);
    }

    [Fact]
    public async Task Security_event_is_committed_before_enter_and_dispose_restores_the_scope()
    {
        var boundary = new Boundary();
        var events = new Events();
        var scope = new Scope(boundary);
        var current = new Current(Access.Platform);
        var service = new PlatformScopeAccessService(current, new Permissions(true),
            events, boundary, TimeProvider.System, new Request(), NullLogger<PlatformScopeAccessService>.Instance);
        var guard = new PlatformActionGuard(service, scope);
        var id = Guid.NewGuid();
        var result = await guard.EnterAsync(id, "Revisar", Ct);
        Assert.True(result.IsSuccess);
        using (result.Value)
        {
            Assert.Equal(id, scope.CurrentId);
            Assert.True(boundary.Committed);
            var ev = Assert.Single(events.Items);
            Assert.Equal(SecurityEventType.PlatformTenantEntered, ev.Type);
            Assert.Equal(id, ev.TargetTenantId);
            Assert.Equal(current.UserId, ev.ActorId);
            Assert.Equal("203.0.113.*", ev.MaskedIpAddress);
            Assert.Equal("{}", ev.Changes);
            Assert.Equal(current.UserId, result.Value.OperatorId);
        }
        Assert.Null(scope.CurrentId);
    }

    [Fact]
    public async Task Consumer_and_missing_reason_are_rejected_before_any_write()
    {
        var boundary = new Boundary();
        var events = new Events();
        foreach (var access in new[] { Access.Consumer, Access.Business })
        {
            var service = new PlatformScopeAccessService(new Current(access), new Permissions(true),
                events, boundary, TimeProvider.System, new Request(), NullLogger<PlatformScopeAccessService>.Instance);
            Assert.True((await service.AuthorizeAsync(Guid.NewGuid(), "Revisar", Ct)).IsFailure);
        }
        var owner = new PlatformScopeAccessService(new Current(Access.Platform), new Permissions(true),
            events, boundary, TimeProvider.System, new Request(), NullLogger<PlatformScopeAccessService>.Instance);
        Assert.True((await owner.AuthorizeAsync(Guid.NewGuid(), " ", Ct)).IsFailure);
        Assert.False(boundary.Committed);
        Assert.Empty(events.Items);
    }

    private sealed class Current(Access access) : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Access? Access => access;
    }

    private sealed class Request : IRequestInfo
    {
        public string? IpAddress => "203.0.113.27";
        public string? UserAgent => null;
    }

    private sealed class Permissions(bool allowed) : IPlatformPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) => Task.FromResult(allowed);
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class Events : ISecurityEventRepository
    {
        public List<SecurityEvent> Items { get; } = [];
        public void Add(SecurityEvent securityEvent) => Items.Add(securityEvent);
    }

    private sealed class Boundary : IUnitOfWork
    {
        public bool Committed { get; private set; }
        public bool Running { get; private set; }
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
            CommitPolicy policy, CancellationToken cancellationToken) where TResult : Result
        {
            Assert.False(Running);
            Running = true;
            var result = await work(cancellationToken);
            Committed = result.IsSuccess;
            Running = false;
            return result;
        }
    }

    private sealed class Scope(Boundary boundary) : ITenantScope
    {
        public int Entries { get; private set; }
        public Guid? CurrentId { get; private set; }
        public IDisposable Enter(Guid tenantId)
        {
            Assert.True(boundary.Committed);
            Assert.False(boundary.Running);
            CurrentId = tenantId;
            Entries++;
            return new Exit(this);
        }
        private sealed class Exit(Scope scope) : IDisposable
        {
            public void Dispose() => scope.CurrentId = null;
        }
    }
}
