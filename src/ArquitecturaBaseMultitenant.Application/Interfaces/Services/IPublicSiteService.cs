using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPublicSiteService
{
    Task<Result<PublicSiteResponse>> GetAsync(CancellationToken cancellationToken);
    Task<Result<PublicSiteLogoResponse>> GetLogoAsync(CancellationToken cancellationToken);
}
