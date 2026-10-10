using ArquitecturaBaseMultitenant.Application.Models.Request;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

public interface IExecutionActorScope
{
    IDisposable Enter(ExecutionActor actor);
}
