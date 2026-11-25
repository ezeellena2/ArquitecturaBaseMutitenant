using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Time;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

/// <summary>271: el cupo diario de la IA corta a la medianoche de la organización, y fuera de una, a la del perfil.</summary>
public sealed class AssistantQuotaDayResolverTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string BuenosAires = "America/Argentina/Buenos_Aires";
    private const string Madrid = "Europe/Madrid";

    [Fact]
    public void The_same_instant_is_another_day_and_another_midnight_in_each_zone()
    {
        var resolver = Resolver(null);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var nowUtc = new DateTime(2026, 10, 11, 2, 0, 0, DateTimeKind.Utc);
        var inBuenosAires = resolver.Of(first, nowUtc, BuenosAires);
        var inMadrid = resolver.Of(second, nowUtc, Madrid);
        Assert.Equal(new AssistantQuotaDay(first, new DateOnly(2026, 10, 10), new DateTime(2026, 10, 11, 3, 0, 0, DateTimeKind.Utc), BuenosAires), inBuenosAires);
        Assert.Equal(new AssistantQuotaDay(second, new DateOnly(2026, 10, 11), new DateTime(2026, 10, 11, 22, 0, 0, DateTimeKind.Utc), Madrid), inMadrid);
        // Una hora después Buenos Aires ya cambió de día y Madrid no: los dos cupos de la misma persona cortan por separado.
        var later = nowUtc.AddHours(1.5);
        Assert.Equal(new DateOnly(2026, 10, 11), resolver.Of(first, later, BuenosAires).Day);
        Assert.Equal(new DateOnly(2026, 10, 11), resolver.Of(second, later, Madrid).Day);
        Assert.Throws<ArgumentException>(() => resolver.Of(first, DateTime.SpecifyKind(nowUtc, DateTimeKind.Local), Madrid));
    }

    [Fact]
    public void The_quota_belongs_to_the_organization_only_in_the_business_access()
    {
        var tenant = Guid.CreateVersion7();
        Assert.Equal(tenant, AssistantQuotaDayResolver.OrganizationOf(Access.Business, tenant));
        Assert.Null(AssistantQuotaDayResolver.OrganizationOf(Access.Consumer, tenant));
        Assert.Null(AssistantQuotaDayResolver.OrganizationOf(Access.Platform, null));
    }

    [Fact]
    public async Task The_organization_zone_cuts_the_business_day_and_the_profile_zone_cuts_the_rest()
    {
        var resolver = Resolver(new TenantSettingsRow(Guid.CreateVersion7(), "es-AR", Madrid, "ARS"));
        Assert.Equal(Madrid, await resolver.ZoneAsync(Access.Business, BuenosAires, Ct));
        Assert.Equal(BuenosAires, await resolver.ZoneAsync(Access.Consumer, BuenosAires, Ct));
        Assert.Equal(BuenosAires, await resolver.ZoneAsync(Access.Platform, BuenosAires, Ct));
        // Sin configuración de la organización, el día es el mismo para todos sus miembros: UTC, nunca el perfil de quien pide.
        Assert.Equal(TimeZoneInfo.Utc.Id, await Resolver(null).ZoneAsync(Access.Business, BuenosAires, Ct));
    }

    private static AssistantQuotaDayResolver Resolver(TenantSettingsRow? settings) =>
        new(null!, new FixedSettings(settings), new TimeZoneService(TimeProvider.System));

    private sealed class FixedSettings(TenantSettingsRow? row) : ITenantSettingsReader
    {
        public Task<TenantSettingsRow?> FindCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(row);
    }
}
