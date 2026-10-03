using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Users;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Platform;

public sealed class PlatformSettingsServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Update_commits_settings_and_operator_event_before_invalidating_the_cache()
    {
        using var fixture = new ServiceFixture<PlatformSettingsService>(new UpdatePlatformSettingsRequestValidator());
        var events = new List<string>();
        var repository = new StubRepository();
        var audit = new StubEvents();
        var unitOfWork = new FakeUnitOfWork(events);
        var service = Create(fixture, repository, audit, unitOfWork, new StubCache(events));

        var result = await service.UpdateAsync(new(ConsumerSignupMode.Closed, BusinessSignupMode.RequiresApproval, 3, 7, "Control del alta"), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(7u, repository.ExpectedVersion);
        Assert.Equal(ConsumerSignupMode.Closed, repository.Settings!.ConsumerSignup);
        Assert.True(repository.Marked);
        Assert.Equal(new[] { "commit", "cache" }, events);
        Assert.Equal(1, unitOfWork.Transactions);
        var securityEvent = Assert.Single(audit.Items);
        Assert.Equal(SecurityEventType.PlatformSettingsChanged, securityEvent.Type);
        Assert.Equal(AuditActorKind.PlatformOperator, securityEvent.ActorKind);
        Assert.Equal("Control del alta", securityEvent.Reason);
        Assert.Equal(fixture.TimeProvider.GetUtcNow().UtcDateTime, securityEvent.OccurredAtUtc);
        Assert.Equal("203.0.113.*", securityEvent.MaskedIpAddress);
        using var changes = JsonDocument.Parse(securityEvent.Changes);
        Assert.Equal(3, changes.RootElement.EnumerateObject().Count());
        Assert.Equal("Open", changes.RootElement.GetProperty("consumerSignup").GetProperty("before").GetString());
        Assert.Equal("Closed", changes.RootElement.GetProperty("consumerSignup").GetProperty("after").GetString());
        Assert.Equal(1, changes.RootElement.GetProperty("maxOwnedOrganizations").GetProperty("before").GetInt32());
        Assert.Equal(3, changes.RootElement.GetProperty("maxOwnedOrganizations").GetProperty("after").GetInt32());
        Assert.DoesNotContain("Control del alta", securityEvent.Changes);
    }

    [Theory]
    [InlineData(0, "Motivo")]
    [InlineData(3, " ")]
    public async Task Invalid_input_never_opens_a_transaction(int limit, string reason)
    {
        using var fixture = new ServiceFixture<PlatformSettingsService>(new UpdatePlatformSettingsRequestValidator());
        var unitOfWork = new FakeUnitOfWork();
        var audit = new StubEvents();
        var service = Create(fixture, new StubRepository(), audit, unitOfWork, new StubCache([]));

        Assert.True((await service.UpdateAsync(new(ConsumerSignupMode.Open, BusinessSignupMode.Open, limit, 1, reason), Ct)).IsFailure);
        Assert.Equal(0, unitOfWork.Transactions);
        Assert.Empty(audit.Items);
    }

    [Fact]
    public async Task A_commit_conflict_does_not_invalidate_the_cache()
    {
        using var fixture = new ServiceFixture<PlatformSettingsService>(new UpdatePlatformSettingsRequestValidator());
        var events = new List<string>();
        var unitOfWork = new FakeUnitOfWork { CommitFailure = new ConcurrencyConflictException() };
        var service = Create(fixture, new StubRepository(), new StubEvents(), unitOfWork, new StubCache(events));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.UpdateAsync(new(ConsumerSignupMode.Open, BusinessSignupMode.Closed, 2, 1, "Cambio"), Ct));
        Assert.Empty(events);
    }

    private static PlatformSettingsService Create(ServiceFixture<PlatformSettingsService> fixture, StubRepository repository,
        StubEvents events, FakeUnitOfWork unitOfWork, StubCache cache) =>
        new(new StubReader(), repository, unitOfWork, fixture.Validator,
            new PlatformSettingsAudit(new StubUser(), events, fixture.TimeProvider, new StubRequest()), cache, fixture.TimeProvider, fixture.Logger);

    private sealed class StubReader : IPlatformSettingsReader
    {
        public Task<PlatformSettingsRow?> FindAsync(CancellationToken ct) => Task.FromResult<PlatformSettingsRow?>(null);
    }

    private sealed class StubRepository : IPlatformSettingsRepository
    {
        public PlatformSettings? Settings { get; } = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Open, 1);
        public uint? ExpectedVersion { get; private set; }
        public bool Marked { get; private set; }
        public Task<PlatformSettings?> GetAsync(CancellationToken ct) => Task.FromResult(Settings);
        public Task<PlatformSettings?> GetForUpdateAsync(uint version, CancellationToken ct) { ExpectedVersion = version; return Task.FromResult(Settings); }
        public void MarkUpdated(PlatformSettings settings) => Marked = true;
        public void Add(PlatformSettings settings) => throw new NotSupportedException();
    }

    private sealed class StubUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.CreateVersion7();
        public Access? Access => Domain.Users.Access.Platform;
    }

    private sealed class StubRequest : IRequestInfo
    {
        public string? IpAddress => "203.0.113.27";
        public string? UserAgent => null;
    }

    private sealed class StubEvents : ISecurityEventRepository
    {
        public List<SecurityEvent> Items { get; } = [];
        public void Add(SecurityEvent item) => Items.Add(item);
    }

    private sealed class StubCache(List<string> events) : IPlatformSettingsCache
    {
        public ValueTask InvalidateAsync(CancellationToken ct) { events.Add("cache"); return ValueTask.CompletedTask; }
    }
}
