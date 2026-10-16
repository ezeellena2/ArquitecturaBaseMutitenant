namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>
/// La acción en curso pide una prueba reciente del segundo factor (E9): la guarda HTTP la comprobó antes de la
/// idempotencia y la unidad de trabajo que escribe la vuelve a exigir, con la sesión y el sello bloqueados.
/// </summary>
public interface IRecentPlatformProofContext
{
    Guid? RequiredSessionId { get; }

    void Require(Guid platformSessionId);
}
