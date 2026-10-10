using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantDoneStreamHttpResponse(AssistantTurnResultResponse Data) : AssistantStreamHttpResponse;
