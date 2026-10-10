using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record UpdateAssistantModelHttpRequest(string? Name, AssistantProvider? Provider, string? ProviderModelId, Money? InputPricePerMillion, Money? OutputPricePerMillion, bool? IsActive, uint? Version, AssistantModelTechnicalHttpRequest? Technical = null);
