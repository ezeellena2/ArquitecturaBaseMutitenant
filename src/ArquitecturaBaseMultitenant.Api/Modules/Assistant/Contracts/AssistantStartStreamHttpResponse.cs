using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantStartStreamHttpResponse(AssistantStartEventResponse Data) : AssistantStreamHttpResponse;
