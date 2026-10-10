using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantNoticeService
{
    /// <summary>Checks current authorization and version, without recording consent. Used for replay and admission.</summary>
    Task<Result> CheckAcceptedAsync(AssistantNoticeRequest request, CancellationToken ct);
}
