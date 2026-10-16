using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AnthropicChatClientFactory(IAssistantConfigurationPolicy policy, IAssistantSettingsReader settings,
    IAssistantKeyProtector protector, IOptions<AssistantOptions> options, ICurrentUser user, IAssistantModelReader models,
    IAssistantChatContext execution, AnthropicChatClientBuilder clients) : IChatClientFactory
{
    public async Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken)
    {
        var resolved = await policy.ResolveAsync(cancellationToken);
        if (resolved.IsFailure) return resolved.Error;
        var configuration = resolved.Value;
        if (execution.Binding is { } binding)
        {
            if (configuration.Mode != binding.Mode) return AssistantErrors.SettingsNotConfigured;
            var admittedModel = (await models.ListAllAsync(cancellationToken)).SingleOrDefault(model => model.Id == binding.ModelId);
            if (admittedModel is null || admittedModel.ProviderModelId != binding.ProviderModelId ||
                !AssistantModelCompatibilityPolicy.Supports(admittedModel, binding.Effort)) return AssistantErrors.ModelNotAllowed;
            if (binding.Mode == AssistantMode.PlatformKey && !(await settings.FindPlatformAsync(cancellationToken)).AllowedModelIds.Contains(binding.ModelId))
                return AssistantErrors.ModelNotAllowed;
            configuration = configuration with { Model = admittedModel, Effort = binding.Effort };
        }
        try
        {
            var material = configuration.Mode == AssistantMode.OwnKey
                ? await settings.GetOrganizationKeyAsync(cancellationToken) : await settings.GetPlatformKeyAsync(cancellationToken);
            var key = material.Ciphertext is not null
                ? configuration.Mode == AssistantMode.OwnKey ? protector.UnprotectOrganization(material.Ciphertext) : protector.UnprotectPlatform(material.Ciphertext)
                : configuration.Mode == AssistantMode.PlatformKey && !material.HasOverride ? options.Value.Platform.ApiKey : null;
            if (string.IsNullOrWhiteSpace(key)) return AssistantErrors.SettingsNotConfigured;
            return Result.Success(clients.Build(key, configuration.Model.ProviderModelId, configuration.Effort, AssistantTechnicalLimits.MaxOutputTokens));
        }
        catch (CryptographicException) { return AssistantErrors.SettingsNotConfigured; }
    }
    public async Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken)
    {
        if (user.UserId is null || user.Access is not (Domain.Users.Access.Business or Domain.Users.Access.Platform))
            throw new InvalidOperationException("Key validation requires an authorized configuration identity.");
        var platform = await settings.FindPlatformAsync(cancellationToken);
        var candidates = (await models.ListAllAsync(cancellationToken)).Where(value => AssistantModelCompatibilityPolicy.Supports(value, AssistantEffort.Low)).ToArray();
        var model = candidates.FirstOrDefault(value => value.Id == platform.DefaultModelId)
            ?? candidates.OrderBy(value => value.InputPricePerMillion.Amount + value.OutputPricePerMillion.Amount).ThenBy(value => value.Id).FirstOrDefault();
        if (model is null) return AssistantErrors.ModelNotAllowed;
        return await clients.ValidateDraftAsync(key, model.ProviderModelId, cancellationToken);
    }
}
