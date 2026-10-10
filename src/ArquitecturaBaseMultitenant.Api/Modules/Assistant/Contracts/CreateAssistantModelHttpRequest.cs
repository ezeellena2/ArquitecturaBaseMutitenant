using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record CreateAssistantModelHttpRequest(string? Name, AssistantProvider? Provider, string? ProviderModelId, Money? InputPricePerMillion, Money? OutputPricePerMillion, bool? IsActive, AssistantModelTechnicalHttpRequest? Technical = null);
