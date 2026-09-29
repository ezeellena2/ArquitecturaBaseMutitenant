using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;

internal sealed class TestCurrentUser(Guid? userId = null, Access? access = null) : ICurrentUser
{
    public Guid? UserId => userId;
    public Access? Access => access;
}
