using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal sealed class Poster : Entity, IPublishedByBusiness
{
    private Poster()
    {
    }

    public Poster(Guid businessTenantId, bool isPublished)
    {
        BusinessTenantId = businessTenantId;
        IsPublished = isPublished;
    }

    public Guid BusinessTenantId { get; private set; }
    public bool IsPublished { get; private set; }
}
