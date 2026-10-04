using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Platform;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Platform;

public sealed class PlatformTenantSlugTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Name_generates_the_canvas_slug_without_accents_or_outer_separators()
    {
        var service = new PlatformTenantSlugResolver(new Slugs());
        Assert.Equal("epsilon-srl", (await service.AllocateAsync(" Épsilon SRL! ", Guid.NewGuid(), Ct)).Value);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Empresa")]
    [InlineData("公司")]
    [InlineData("ab")]
    public async Task Reserved_taken_or_non_ascii_names_get_a_valid_unique_address(string name)
    {
        var id = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var service = new PlatformTenantSlugResolver(new Slugs());
        var slug = await service.AllocateAsync(name, id, Ct);
        Assert.True(slug.IsSuccess);
        Assert.Contains(id.ToString("N"), slug.Value);
    }

    private sealed class Slugs : ITenantSlugAvailabilityReader
    {
        public Task<bool> IsReservedAsync(string slug, CancellationToken ct) => Task.FromResult(slug == "admin");
        public Task<bool> IsTakenAsync(string slug, CancellationToken ct) => Task.FromResult(slug == "empresa");
        public Task<bool> IsTakenByOtherAsync(string slug, Guid tenantId, CancellationToken ct) => IsTakenAsync(slug, ct);
    }
}
