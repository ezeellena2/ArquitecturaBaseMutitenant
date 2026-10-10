using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsSelectionPolicy(IAssistantModelReader models,
    IAssistantProviderCatalog references, AssistantSettingsBuilder builder)
{
    internal Result ValidateOrganization(UpdateOrganizationAssistantSettingsRequest request, OrganizationAssistantSettingsResponse options)
    {
        var eligible = request.Mode == AssistantMode.OwnKey ? options.OwnKeyModels : options.PlatformKeyModels;
        var model = eligible.SingleOrDefault(value => value.Id == request.ModelId);
        return model is not null && references.Find(model.ProviderModelId) is { } reference && reference.Efforts.Contains(request.Effort!.Value)
            ? Result.Success() : AssistantErrors.ModelNotAllowed;
    }

    internal async Task<Result> ValidatePlatformAsync(UpdatePlatformAssistantSettingsRequest request, CancellationToken ct)
    {
        var allowedIds = request.AllowedModelIds!;
        var compatible = (await models.ListAllAsync(ct)).Where(value => value.IsActive && builder.IsCompatible(value)).ToArray();
        var selected = compatible.SingleOrDefault(value => value.Id == request.DefaultModelId);
        return selected is not null && allowedIds.All(id => compatible.Any(value => value.Id == id))
            && allowedIds.Contains(selected.Id) && references.Find(selected.ProviderModelId)!.Efforts.Contains(request.Effort!.Value)
            ? Result.Success() : AssistantErrors.ModelNotAllowed;
    }
}
