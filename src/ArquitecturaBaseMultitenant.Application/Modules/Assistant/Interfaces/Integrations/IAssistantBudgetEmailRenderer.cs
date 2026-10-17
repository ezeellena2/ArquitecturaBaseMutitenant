using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

public interface IAssistantBudgetEmailRenderer
{
    Task<EmailMessage> RenderAsync(string recipient, int threshold, Money used, Money budget, string culture, string zone, CancellationToken ct);
}
