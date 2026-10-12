using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantExportService
{
    Task<Result<AssistantFileResponse>> ExportAsync(Guid id, AssistantExportFormat? format, CancellationToken ct);
}
