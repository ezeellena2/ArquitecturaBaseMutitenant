using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

/// <summary>Technical accounting closure inside the caller's existing UoW; never authorizes or reads chats.</summary>
public interface IAssistantUsageFinalizer
{
    Task<Result> CompleteAsync(AssistantFinancialBinding binding, Guid operationId, CancellationToken ct);
}
