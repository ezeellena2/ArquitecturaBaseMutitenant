using ArquitecturaBaseMultitenant.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;

internal sealed class AuditTestDbContext(DbContextOptions<AuditTestDbContext> options) : DbContext(options)
{
    public DbSet<AuditedRecord> Records => Set<AuditedRecord>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AuditedRecord>().HasKey(record => record.Id);
        builder.Entity<AuditedPublicRecord>().HasKey(record => record.Id);
        builder.Entity<AuditedSharedRecord>().HasKey(record => record.Id);
        builder.Entity<AuditEntry>().HasKey(entry => entry.Id);
    }
}
