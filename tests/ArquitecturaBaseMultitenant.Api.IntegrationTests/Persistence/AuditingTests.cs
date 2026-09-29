using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class AuditingTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Added_row_receives_creation_marks_from_time_provider_and_current_user()
    {
        var actorId = Guid.NewGuid();
        using var context = CreateContext();
        var record = new AuditedRecord(Guid.NewGuid(), "Widget", "secret", "person@example.com");
        context.Add(record);

        var interceptor = new AuditableEntityInterceptor(new TestCurrentUser(actorId), new FakeTimeProvider(Instant));
        interceptor.SavingChanges(Event(context), default);

        Assert.Equal(Instant.UtcDateTime, record.CreatedAtUtc);
        Assert.Equal(actorId, record.CreatedBy);
        Assert.Null(record.ModifiedAtUtc);
    }

    [Fact]
    public async Task Modified_row_receives_modification_marks_without_changing_creation_marks()
    {
        var actorId = Guid.NewGuid();
        using var context = CreateContext();
        var record = new AuditedRecord(Guid.NewGuid(), "Before", "secret", "person@example.com");
        context.Attach(record);
        record.Rename("After");

        var interceptor = new AuditableEntityInterceptor(new TestCurrentUser(actorId), new FakeTimeProvider(Instant));
        await interceptor.SavingChangesAsync(Event(context), default, TestContext.Current.CancellationToken);

        Assert.Equal(Instant.UtcDateTime, record.ModifiedAtUtc);
        Assert.Equal(actorId, record.ModifiedBy);
        Assert.Equal(default, record.CreatedAtUtc);
        Assert.Null(record.CreatedBy);
    }

    private static AuditTestDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseNpgsql("Host=localhost;Database=auditing_model_test")
            .Options);

    private static DbContextEventData Event(DbContext context) =>
        new(null!, (_, _) => string.Empty, context);
}
