using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;

/// <summary>
/// Proporciona usuario y acceso controlados por cada test de auditoría. Permite comprobar actor humano,
/// operador o sistema sin una sesión HTTP.
/// </summary>
internal sealed class TestCurrentUser(Guid? userId = null, Access? access = null) : ICurrentUser
{
    public Guid? UserId => userId;
    public Access? Access => access;
}
