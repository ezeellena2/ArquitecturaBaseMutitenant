using ArquitecturaBaseMultitenant.Domain.PublicSite;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IPublicPageRepository
{
    Task<PublicPage?> GetCurrentAsync(uint expectedVersion, CancellationToken cancellationToken);

    void Add(PublicPage page);

    void MarkUpdated(PublicPage page);
}
