using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record UpdateAssistantModelRequest(string? Name, AssistantProvider? Provider, string? ProviderModelId, Money? InputPricePerMillion, Money? OutputPricePerMillion, bool? IsActive, uint? Version, AssistantModelTechnicalRequest? Technical = null);
