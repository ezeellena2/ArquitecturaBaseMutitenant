using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
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

    /// <summary>
    /// ADR 0068 §8: una propuesta con su mensaje del asistente, sembrada en la conversación. Los sujetos van al mensaje, y
    /// <paramref name="shape"/> la deja reemplazada, aplicada o con intentos antes de guardarla.
    /// </summary>
    internal static async Task<AssistantProposal> ProposalAsync(ApiFactory factory, Guid tenantId, Guid conversationId, Guid owner, string action,
        int version, AssistantProposalValues values, Guid? targetId = null, uint? baseVersion = null, IReadOnlyCollection<Guid>? subjects = null,
        Action<AssistantProposal, DateTime>? shape = null)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var message = AssistantMessage.Create(conversationId, "Assistant", "Complete", "Te preparé la propuesta.", nowUtc);
        message.SetPrivacySubjects(subjects ?? []);
        var proposal = AssistantProposal.Create(Guid.CreateVersion7(), conversationId, message.Id, owner, action, version,
            AssistantTurnJson.Serialize(values), targetId, baseVersion, nowUtc);
        shape?.Invoke(proposal, nowUtc);
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().AddRange(message, proposal);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess, saved.Error.Code);
        Assert.Equal(tenantId, proposal.TenantId);
        return proposal;
    }

    /// <summary>Lo que escribió el modelo para un campo y el id resuelto, como lo guarda la propuesta.</summary>
    internal static AssistantActionResolvedValue Written(string field, string written, string? resolved = null) =>
        new(field, JsonSerializer.SerializeToElement(written), resolved is null ? null : JsonSerializer.SerializeToElement(resolved), null);
}
