using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;

internal sealed class TestCurrentUser(Guid? userId = null, AccessKind? access = null) : ICurrentUser
{
    public Guid? UserId => userId;
    public AccessKind? Access => access;
}
