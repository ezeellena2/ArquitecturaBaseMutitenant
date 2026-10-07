using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>Ingreso real Consumer/Business; la preparación de filas también pasa por la UoW y el sello de tenant.</summary>
internal sealed class PersonalIsolationJourney(PersonalIsolationFactory factory, HttpClient client, AccountJourney account,
    Guid personalId) : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal HttpClient Client => client;
    internal Guid PersonalId => personalId;

    internal static async Task<PersonalIsolationJourney> CreateAsync(PersonalIsolationFactory factory)
    {
        var client = factory.Client();
        try
        {
            var account = await AccountJourney.RegisterAsync(factory.Host, client, Ct);
            return new PersonalIsolationJourney(factory, client, account, await ActiveTenantAsync(client));
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    internal async Task ConsumerAsync()
    {
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        Assert.Equal(personalId, await ActiveTenantAsync(client));
    }

    internal async Task BusinessAsync(Guid businessId)
    {
        await AccountJourney.AuthorizeAsync(client, "business", Ct, businessId);
        Assert.Equal(businessId, await ActiveTenantAsync(client));
    }

    internal async Task<Guid> CreateBusinessAsync()
    {
        var business = Tenant.CreateBusiness("Organización del test personal", requiresApproval: false);
        Assert.True(business.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(business.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId], account.UserId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return business.Id;
    }

    internal async Task<Widget> AddBusinessWidgetAsync(Guid businessId, string name)
    {
        var widget = new Widget(name);
        Assert.Equal(Guid.Empty, widget.TenantId);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(businessId, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<ApplicationDbContext>().Set<Widget>().Add(widget);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(businessId, widget.TenantId);
        return widget;
    }

    internal static async Task<Guid> ActiveTenantAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return body!.RootElement.GetProperty("activeTenantId").GetGuid();
    }

    public void Dispose() => client.Dispose();
}
