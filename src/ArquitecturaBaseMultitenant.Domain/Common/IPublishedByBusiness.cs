namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Dato público perteneciente a la página de una organización.</summary>
public interface IPublishedByBusiness
{
    Guid BusinessTenantId { get; }

    bool IsPublished { get; }
}
