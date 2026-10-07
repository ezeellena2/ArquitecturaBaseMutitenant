using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

internal sealed class DirectoryService(IDirectoryReader directory, IRequestValidator validator,
    TimeProvider timeProvider, ILogger<DirectoryService> logger) : IDirectoryService
{
    public Task<Result<PagedResult<DirectoryEntryRow>>> ListAsync(ListDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<PagedResult<DirectoryEntryRow>>(logger, timeProvider, "ListDirectory", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await directory.ListAsync(request, cancellationToken);
        });
    }

    public Task<Result<PublicSiteLogoResponse>> GetLogoAsync(string slug, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PublicSiteLogoResponse>(logger, timeProvider, "GetDirectoryLogo", async () =>
        {
            var logo = await directory.FindLogoAsync(slug, cancellationToken);
            return logo is null ? PublicPageErrors.NotFound : logo;
        });
}
