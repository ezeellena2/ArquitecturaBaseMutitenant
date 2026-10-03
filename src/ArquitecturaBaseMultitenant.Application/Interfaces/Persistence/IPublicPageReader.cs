using ArquitecturaBaseMultitenant.Application.Models.PublicSite;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IPublicPageReader
{
    Task<PublicPageRow?> FindCurrentAsync(CancellationToken cancellationToken);
}
