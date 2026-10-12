using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

public interface IAssistantChatContext
{
    AssistantChatBinding? Binding { get; set; }
}
