using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
internal sealed class AssistantChatContext : IAssistantChatContext
{
    public AssistantChatBinding? Binding { get; set; }
}
