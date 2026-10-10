using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantModelWriter(IAssistantModelRepository repository, AssistantSettingsAuditWriter audit)
{
    internal async Task<Result<AssistantModelPrice>> CreateAsync(CreateAssistantModelRequest request, CancellationToken ct)
    {
        if (await repository.ExistsAsync(request.Provider!.Value.ToString(), request.ProviderModelId!, null, ct)) return AssistantErrors.ModelAlreadyExists;
        var model = AssistantModelPrice.Create(request.Provider.Value.ToString(), request.ProviderModelId!, request.Name!, request.InputPricePerMillion!, request.OutputPricePerMillion!);
        model.Update(model.Provider, model.ProviderModelId, model.DisplayName, model.InputPrice, model.OutputPrice, request.IsActive!.Value);
        repository.Add(model);
        audit.RecordGlobal("Assistant model created", new SecurityEventChanges());
        return model;
    }
    internal async Task<Result<AssistantModelPrice>> UpdateAsync(Guid id, UpdateAssistantModelRequest request, CancellationToken ct)
    {
        var model = await repository.GetAsync(id, request.Version!.Value, ct);
        if (model is null) return AssistantErrors.ModelNotFound;
        if (await repository.ExistsAsync(request.Provider!.Value.ToString(), request.ProviderModelId!, id, ct)) return AssistantErrors.ModelAlreadyExists;
        var changes = new SecurityEventChanges();
        changes.AddBoolean("assistant.modelActive", model.IsEnabled, request.IsActive);
        changes.AddBoolean("assistant.pricesChanged", false, model.InputPrice != request.InputPricePerMillion || model.OutputPrice != request.OutputPricePerMillion);
        changes.AddBoolean("assistant.modelIdentifierChanged", false, model.ProviderModelId != request.ProviderModelId);
        model.Update(request.Provider.Value.ToString(), request.ProviderModelId!, request.Name!, request.InputPricePerMillion!, request.OutputPricePerMillion!, request.IsActive!.Value);
        repository.MarkUpdated(model);
        audit.RecordGlobal("Assistant model updated", changes);
        return model;
    }
}
