using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Captura el acceso del servidor y revalida el binding de una operación privada antes del scope de worker.</summary>
public interface IExecutionActorFactory
{
    Task<Result<ExecutionActor>> CaptureAsync(CancellationToken cancellationToken);

    Task<Result<ExecutionActor>> RevalidateAsync(ExecutionActor actor, CancellationToken cancellationToken);
}
