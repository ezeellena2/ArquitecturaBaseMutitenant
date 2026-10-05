using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPublicPageService
{
    Task<Result<PublicPageResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result<VersionResponse>> UpdateAsync(UpdatePublicPageRequest request, CancellationToken cancellationToken);

    Task<Result<VersionResponse>> PublishAsync(SetPublicPagePublicationRequest request, CancellationToken cancellationToken);

    Task<Result<VersionResponse>> UnpublishAsync(SetPublicPagePublicationRequest request, CancellationToken cancellationToken);
}
