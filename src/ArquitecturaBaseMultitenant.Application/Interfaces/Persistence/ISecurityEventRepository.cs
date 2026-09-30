using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Agrega eventos de seguridad de alcance global o tenant dentro de la transacción que ejecuta el cambio.</summary>
public interface ISecurityEventRepository
{
    void Add(SecurityEvent securityEvent);
}
