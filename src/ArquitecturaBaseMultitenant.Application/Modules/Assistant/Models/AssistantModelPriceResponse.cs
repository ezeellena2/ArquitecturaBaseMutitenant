using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantModelPriceResponse(Guid Id, string Name, AssistantProvider Provider, string ProviderModelId, Money InputPricePerMillion, Money OutputPricePerMillion, bool IsActive, uint Version,
    AssistantModelTechnicalResponse? Technical = null, string? TechnicalRevision = null);
