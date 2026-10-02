using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Comprueba la persistencia versionada y el aislamiento real de Companies en PostgreSQL.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class CompanyPersistenceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Company_rows_are_isolated_by_tenant_and_store_optional_tax_id()
    {
        var firstTenant = await CreateBusinessTenantAsync("Empresa de prueba A");
        var otherTenant = await CreateBusinessTenantAsync("Empresa de prueba B");
        var company = Company.Create("  Taller Norte  ", taxId: null, "UTC").Value;
        await AddCompanyAsync(firstTenant.Id, company);

        await using var firstConnection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString,
            firstTenant.Id, Ct);
        await using var otherConnection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString,
            otherTenant.Id, Ct);

        Assert.Equal(1, await CountAsync(firstConnection, company.Id));
        Assert.Equal(0, await CountAsync(otherConnection, company.Id));
        Assert.True(company.Version > 0);
        Assert.True(company.IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(firstTenant.Id);
        var stored = await services.GetRequiredService<ApplicationDbContext>().Companies
            .SingleAsync(item => item.Id == company.Id, Ct);
        Assert.Equal("Taller Norte", stored.Name);
        Assert.Null(stored.TaxId);
        Assert.True(stored.IsActive);
    }

    private async Task<Tenant> CreateBusinessTenantAsync(string name)
    {
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant;
    }

    private async Task AddCompanyAsync(Guid tenantId, Company company)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ICompanyRepository>().Add(company);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<int> CountAsync(NpgsqlConnection connection, Guid companyId)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM tenant.\"Companies\" WHERE \"Id\" = @id",
            connection);
        command.Parameters.AddWithValue("id", companyId);
        return checked((int)(long)(await command.ExecuteScalarAsync(Ct))!);
    }
}
