using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>
/// Representa una publicación sintética de una empresa. Permite probar que la lectura pública dependa del
/// espacio publicador y de la marca de publicación.
/// </summary>
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
