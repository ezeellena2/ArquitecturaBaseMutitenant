using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

/// <summary>Installed only by ConfigureTestServices; production has no fake provider path.</summary>
public static class AssistantFakeChatRegistration
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<AssistantFakeChatMetrics>();
        services.RemoveAll<IChatClientFactory>();
        services.AddScoped<IChatClientFactory, FakeFactory>();
    }

    private sealed class FakeFactory(IAssistantConfigurationPolicy policy, IAssistantSettingsReader settings,
        IAssistantModelReader models, IAssistantChatContext execution, AssistantFakeChatMetrics metrics, TimeProvider clock) : IChatClientFactory
    {
        public async Task<Result<IChatClient>> CreateAsync(CancellationToken ct)
        {
            var resolved = await policy.ResolveAsync(ct);
            if (resolved.IsFailure) return resolved.Error;
            if (execution.Binding is { } binding)
            {
                if (resolved.Value.Mode != binding.Mode) return AssistantErrors.SettingsNotConfigured;
                var model = (await models.ListAllAsync(ct)).SingleOrDefault(value => value.Id == binding.ModelId);
                if (model is null || model.ProviderModelId != binding.ProviderModelId || !AssistantModelCompatibilityPolicy.Supports(model, binding.Effort))
                    return AssistantErrors.ModelNotAllowed;
                if (binding.Mode == AssistantMode.PlatformKey && !(await settings.FindPlatformAsync(ct)).AllowedModelIds.Contains(binding.ModelId))
                    return AssistantErrors.ModelNotAllowed;
            }
            return Result.Success<IChatClient>(new FakeChatClient(metrics, clock));
        }
        public Task<Result> ValidateKeyAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success());
        }
    }
}
