using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba que eliminar se convierta en una marca con instante UTC y actor. Incluye la ejecución del
/// sistema cuando no hay usuario en sesión.
/// </summary>
public sealed class SoftDeleteTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Delete_becomes_a_modified_row_with_a_utc_tombstone_and_actor()
    {
        var actorId = Guid.NewGuid();
        using var context = CreateContext();
        var record = new AuditedRecord(Guid.NewGuid(), "Widget", "secret", "person@example.com");
        context.Attach(record);
        context.Remove(record);

        var interceptor = new SoftDeleteInterceptor(new TestCurrentUser(actorId), new FakeTimeProvider(Instant));
        interceptor.SavingChanges(Event(context), default);

        Assert.Equal(EntityState.Modified, context.Entry(record).State);
        Assert.True(record.IsDeleted);
        Assert.Equal(Instant.UtcDateTime, record.DeletedAtUtc);
        Assert.Equal(actorId, record.DeletedBy);
        Assert.False(context.Entry(record).Property(nameof(AuditedRecord.Name)).IsModified);
        Assert.False(context.Entry(record).Property(nameof(AuditedRecord.Secret)).IsModified);
        Assert.False(context.Entry(record).Property(nameof(AuditedRecord.ContactEmail)).IsModified);
        Assert.True(context.Entry(record).Property(nameof(AuditedRecord.IsDeleted)).IsModified);
    }

    [Fact]
    public async Task Async_delete_uses_system_actor_when_no_user_is_present()
    {
        using var context = CreateContext();
        var record = new AuditedRecord(Guid.NewGuid(), "Widget", "secret", "person@example.com");
        context.Attach(record);
        context.Remove(record);

        var interceptor = new SoftDeleteInterceptor(new TestCurrentUser(), new FakeTimeProvider(Instant));
        await interceptor.SavingChangesAsync(Event(context), default, TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Modified, context.Entry(record).State);
        Assert.True(record.IsDeleted);
        Assert.Null(record.DeletedBy);
    }

    private static AuditTestDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseNpgsql("Host=localhost;Database=soft_delete_model_test")
            .Options);

    private static DbContextEventData Event(DbContext context) =>
        new(null!, (_, _) => string.Empty, context);
}
