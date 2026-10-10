using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

internal static class AssistantTestData
{
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static HttpClient Client(ApiFactory factory) => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    internal static async Task<Guid> ActiveTenantAsync(HttpClient client)
    {
        using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        return me!.RootElement.GetProperty("activeTenantId").GetGuid();
    }
    internal static async Task EnableAsync(ApiFactory factory, Guid tenantId, DateTime? expiry = null)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(TenantFeature.Create(tenantId, AssistantFeature.Key, true,
                expiry is null ? TenantFeatureSource.Manual : TenantFeatureSource.Trial, expiry).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty,
                tenantId, "Assistant fixture grant", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        await services.GetRequiredService<ITenantFeatureCache>().InvalidateAsync(tenantId, Ct);
    }
    internal static async Task PersonalAsync(ApiFactory factory, bool enabled)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var settings = await context.Set<AssistantPlatformSettings>().SingleOrDefaultAsync(ct);
            if (settings is null) context.Add(AssistantPlatformSettings.Create(enabled, new Money(0m, CurrencyCode.Create(AssistantContractPolicy.BillingCurrency).Value)));
            else context.Entry(settings).Property(value => value.PersonalEnabled).CurrentValue = enabled;
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty,
                "Assistant fixture personal availability", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }
    internal static async Task<AssistantConversation> ConversationAsync(ApiFactory factory, Guid tenantId, Guid owner, Access access)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var conversation = AssistantConversation.Create(owner, access, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        Assert.Equal(Guid.Empty, conversation.TenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(conversation);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.Equal(tenantId, conversation.TenantId);
        Assert.NotEqual(0u, conversation.Version);
        return conversation;
    }
}
