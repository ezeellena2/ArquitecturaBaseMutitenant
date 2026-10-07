using ArquitecturaBaseMultitenant.Application.Models.PublicSite;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Resuelve un slug a una página publicada de una organización activa.</summary>
public interface IPublicSiteReader
{
    Task<PublicSiteRow?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken);
}
