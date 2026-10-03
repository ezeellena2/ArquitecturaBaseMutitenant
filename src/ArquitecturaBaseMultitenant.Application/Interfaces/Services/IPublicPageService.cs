using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPublicPageService
{
    Task<Result<PublicPageResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result> UpdateAsync(UpdatePublicPageRequest request, CancellationToken cancellationToken);

    Task<Result> PublishAsync(SetPublicPagePublicationRequest request, CancellationToken cancellationToken);

    Task<Result> UnpublishAsync(SetPublicPagePublicationRequest request, CancellationToken cancellationToken);
}
