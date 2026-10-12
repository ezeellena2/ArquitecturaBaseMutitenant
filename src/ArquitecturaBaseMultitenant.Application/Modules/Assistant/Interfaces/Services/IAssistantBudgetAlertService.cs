using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
public interface IAssistantBudgetAlertService
{
    Task<Result> RetryAsync(Guid alertId, CancellationToken ct);
}
