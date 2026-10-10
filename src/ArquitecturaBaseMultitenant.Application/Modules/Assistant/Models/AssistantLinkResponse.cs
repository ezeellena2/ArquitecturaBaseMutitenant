namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantLinkResponse(AssistantRouteKey RouteKey, AssistantLinkParametersResponse Parameters, AssistantLinkQueryResponse Query, string LabelKey);
