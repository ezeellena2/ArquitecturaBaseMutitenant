using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantFinancePrecisionTests(ApiFactory factory)
{
    [Fact]
    public async Task Technical_cost_preserves_twelve_decimals_in_the_existing_global_table()
    {
        var cost = new Money(0.000000123456m, CurrencyCode.Create("USD").Value);
        var row = PlatformAssistantUsageDaily.Create(Guid.CreateVersion7(), new DateOnly(2026, 10, 10), cost);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(row);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var reread = factory.Services.CreateScope();
        var saved = await reread.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<PlatformAssistantUsageDaily>().AsNoTracking().SingleAsync(value => value.Id == row.Id, AssistantTestData.Ct);
        Assert.Equal(cost, saved.Cost);
    }
}
