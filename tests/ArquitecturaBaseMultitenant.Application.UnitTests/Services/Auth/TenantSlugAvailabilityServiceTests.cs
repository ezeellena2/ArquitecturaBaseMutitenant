using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class TenantSlugAvailabilityServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Check_normalizes_before_looking_up_reserved_or_taken_slugs()
    {
        var reader = new FakeReader { Reserved = "admin", Taken = "empresa" };
        var service = CreateService(reader);

        var reserved = await service.CheckAsync(" ADMIN ", Ct);
        var taken = await service.CheckAsync(" EMPRESA ", Ct);
        var free = await service.CheckAsync(" Nueva-Empresa ", Ct);

        Assert.Equal(new TenantSlugAvailabilityResponse("admin", TenantSlugAvailability.Reserved),
            reserved.Value);
        Assert.Equal(new TenantSlugAvailabilityResponse("empresa", TenantSlugAvailability.Taken), taken.Value);
        Assert.Equal(new TenantSlugAvailabilityResponse("nueva-empresa", TenantSlugAvailability.Available),
            free.Value);
        Assert.DoesNotContain("admin", reader.TakenLookups);
        Assert.Contains("nueva-empresa", reader.TakenLookups);
    }

    [Fact]
    public async Task Invalid_slug_is_rejected_without_reading_catalogs()
    {
        var reader = new FakeReader();
        var result = await CreateService(reader).CheckAsync("a b", Ct);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantSlugErrors.InvalidCode, result.Error.Code);
        Assert.Empty(reader.ReservedLookups);
        Assert.Empty(reader.TakenLookups);
    }

    private static TenantSlugAvailabilityService CreateService(ITenantSlugAvailabilityReader reader) =>
        new(reader, TimeProvider.System, NullLogger<TenantSlugAvailabilityService>.Instance);

    private sealed class FakeReader : ITenantSlugAvailabilityReader
    {
        public string? Reserved { get; init; }
        public string? Taken { get; init; }
        public List<string> ReservedLookups { get; } = [];
        public List<string> TakenLookups { get; } = [];

        public Task<bool> IsReservedAsync(string normalizedSlug, CancellationToken cancellationToken)
        {
            ReservedLookups.Add(normalizedSlug);
            return Task.FromResult(normalizedSlug == Reserved);
        }

        public Task<bool> IsTakenAsync(string normalizedSlug, CancellationToken cancellationToken)
        {
            TakenLookups.Add(normalizedSlug);
            return Task.FromResult(normalizedSlug == Taken);
        }

        public Task<bool> IsTakenByOtherAsync(string normalizedSlug, Guid tenantId,
            CancellationToken cancellationToken) => Task.FromResult(normalizedSlug == Taken);
    }
}
