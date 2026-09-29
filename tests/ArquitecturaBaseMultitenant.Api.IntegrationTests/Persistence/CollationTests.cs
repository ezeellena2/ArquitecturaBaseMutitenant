using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class CollationTests(ApiFactory factory)
{
    [Fact]
    public async Task Icu_es_ar_orders_accented_letters_and_enye_without_manual_collation()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);
        foreach (var name in new[] { "Zapata", "Ñandú", "Nuñez", "Álvarez" })
        {
            await fixture.AddAsync(tenantId, Guid.CreateVersion7(), name,
                new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc), cancellationToken);
        }

        var ordered = await fixture.Rows.OrderBy(widget => widget.Name)
            .Select(widget => widget.Name).ToArrayAsync(cancellationToken);

        Assert.Equal(["Álvarez", "Nuñez", "Ñandú", "Zapata"], ordered);
    }
}
