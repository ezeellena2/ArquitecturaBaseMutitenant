namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

// Local composition only. Conversation admission, binding and replay remain T04b/T05.
public sealed record AssistantQuickCompositionResponse(string Title, AssistantQuickResultResponse Result);
