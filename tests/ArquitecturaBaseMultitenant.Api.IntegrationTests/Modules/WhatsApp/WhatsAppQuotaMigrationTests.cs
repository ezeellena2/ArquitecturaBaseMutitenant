using System.ComponentModel.DataAnnotations;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppQuotaMigrationTests
{
    [Fact]
    public async Task Upgrade_and_down_up_preserve_spent_capacity_with_unknown_history_and_runtime_IP_grants()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(); await factory.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(factory.AdminConnectionString); options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new TenantContext());
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, name => name.EndsWith("_WhatsAppProtectedCodeQuota", StringComparison.Ordinal));
        Assert.True(index > 0); var previous = migrations[index - 1]; var current = migrations[index]; var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(previous, ct);
        var id = Guid.CreateVersion7(); var day = new DateOnly(2026, 10, 13);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity."WhatsAppCodeQuotas" ("Id", "PhoneNumberId", "DayUtc", "ReservedCount")
            VALUES ({id}, 'upgrade-number', {day}, 98);
            """, ct);
        await migrator.MigrateAsync(current, ct); await AssertPreservedAsync();
        await migrator.MigrateAsync(previous, ct); await migrator.MigrateAsync(current, ct); await AssertPreservedAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        await using var runtime = new NpgsqlConnection(factory.RuntimeConnectionString); await runtime.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT relrowsecurity, relforcerowsecurity,
                has_table_privilege(current_user, oid, 'SELECT, INSERT, UPDATE, DELETE')
            FROM pg_class WHERE oid='identity."WhatsAppCodeQuotaIps"'::regclass;
            """, runtime);
        await using var reader = await command.ExecuteReaderAsync(ct); Assert.True(await reader.ReadAsync(ct));
        Assert.False(reader.GetBoolean(0)); Assert.False(reader.GetBoolean(1)); Assert.True(reader.GetBoolean(2));
        async Task AssertPreservedAsync()
        {
            var quota = await db.Set<WhatsAppCodeQuota>().AsNoTracking().SingleAsync(row => row.Id == id, ct);
            Assert.Equal(98, quota.ReservedCount); Assert.Equal(98, quota.NewReservedCount); Assert.Equal(98, quota.LegacyReservedCount);
            var nowUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            Assert.True(quota.Reserve(100, 80, false, 0, 20, nowUtc).IsFailure);
            Assert.Equal(98, quota.ReservedCount);
            Assert.True(quota.Reserve(100, 80, true, 0, 20, nowUtc).IsSuccess);
            Assert.True(quota.Reserve(100, 80, true, 0, 20, nowUtc).IsSuccess);
            Assert.True(quota.Reserve(100, 80, true, 0, 20, nowUtc).IsFailure);
            Assert.Equal(100, quota.ReservedCount);
            Assert.Empty(await db.Set<WhatsAppCodeQuotaIp>().AsNoTracking().ToArrayAsync(ct));
        }
    }
    [Fact]
    public void HMAC_is_replica_stable_mapped_IP_canonical_and_separated_by_number_day_and_secret()
    {
        var key = Convert.ToBase64String(Enumerable.Repeat((byte)5, 32).ToArray());
        var a = new WhatsAppQuotaIpHasher(Options.Create(new LoginCodeHashOptions { HashKey = key }));
        var b = new WhatsAppQuotaIpHasher(Options.Create(new LoginCodeHashOptions { HashKey = key }));
        var other = new WhatsAppQuotaIpHasher(Options.Create(new LoginCodeHashOptions { HashKey = Convert.ToBase64String(Enumerable.Repeat((byte)6, 32).ToArray()) }));
        var day = new DateOnly(2026, 10, 13); var value = a.Hash("192.0.2.1", "number", day);
        Assert.Equal(value, b.Hash("::ffff:192.0.2.1", "number", day));
        Assert.Equal(a.Hash(null, "number", day), b.Hash("invalid", "number", day));
        Assert.NotEqual(value, a.Hash("192.0.2.1", "other", day)); Assert.NotEqual(value, a.Hash("192.0.2.1", "number", day.AddDays(1)));
        Assert.NotEqual(value, other.Hash("192.0.2.1", "number", day)); Assert.DoesNotContain("192.0.2.1", value);
    }
    [Theory]
    [InlineData(0, 20, "0.2", false)] [InlineData(100, 0, "0.2", false)]
    [InlineData(100, 20, "-0.01", false)] [InlineData(100, 20, "1.01", false)]
    [InlineData(1, 20, "0.2", true)] [InlineData(7, 3, "0.5", true)]
    public void Quota_configuration_validates_total_IP_cap_and_protected_fraction(int total, int ip, string fraction, bool valid)
    {
        var options = new WhatsAppOptions { DailyAuthCodeLimit = total, DailyNewAuthCodeIpLimit = ip,
            ProtectedAuthCodeFraction = decimal.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture) };
        Assert.Equal(valid, Validator.TryValidateObject(options, new ValidationContext(options), [], true));
    }
}