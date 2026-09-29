using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ISecurityEventRepository
{
    void Add(SecurityEvent securityEvent);
}
