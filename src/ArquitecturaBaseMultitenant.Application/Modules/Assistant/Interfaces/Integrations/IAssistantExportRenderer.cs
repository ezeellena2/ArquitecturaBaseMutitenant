using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

public interface IAssistantExportRenderer
{
    AssistantFileResponse Render(AssistantConversationSummaryResponse conversation, IReadOnlyList<AssistantMessageResponse> messages, AssistantExportFormat format);
}
