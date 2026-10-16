using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>
/// La sesión de Plataforma cuya prueba reciente exige la acción en curso (E9). La deja la guarda HTTP después de
/// comprobarla y la lee la unidad de trabajo al abrir cada transacción del pedido.
/// </summary>
internal sealed class RecentPlatformProofContext : IRecentPlatformProofContext
{
    public Guid? RequiredSessionId { get; private set; }

    public void Require(Guid platformSessionId)
    {
        if (platformSessionId == Guid.Empty)
            throw new ArgumentException("The platform session id cannot be empty.", nameof(platformSessionId));
        RequiredSessionId = platformSessionId;
    }
}
