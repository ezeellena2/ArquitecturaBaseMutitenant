using ArquitecturaBaseMultitenant.Application.Models.Request;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

public interface IExecutionActorContext
{
    ExecutionActor? Actor { get; }
}
