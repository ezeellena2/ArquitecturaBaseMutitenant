using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record CreateAssistantModelRequest(string? Name, AssistantProvider? Provider, string? ProviderModelId, Money? InputPricePerMillion, Money? OutputPricePerMillion, bool? IsActive, AssistantModelTechnicalRequest? Technical = null);
