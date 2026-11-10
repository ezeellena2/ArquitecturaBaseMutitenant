using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

/// <summary>Los cuatro avisos del tope del tablero Mensajes, en la cultura de quien lo recibe.</summary>
public interface IAssistantBudgetEmailRenderer
{
    Task<EmailMessage> RenderAsync(AssistantBudgetAlertNotice notice, CancellationToken ct);
}
