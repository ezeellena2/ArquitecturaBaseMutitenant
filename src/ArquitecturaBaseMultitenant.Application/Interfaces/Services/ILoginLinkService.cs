using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ILoginLinkService
{
    Task<Result<LoginLinkPreviewResponse>> PreviewAsync(PreviewLoginLinkRequest request, CancellationToken ct);
    Task<Result<LoginLinkRedeemResponse>> RedeemAsync(RedeemLoginLinkRequest request, CancellationToken ct);
}
