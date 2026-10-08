using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Esqueleto T01; T07 implementa preview/redeem y retira las respuestas temporales 501.</summary>
internal sealed class LoginLinkService(TimeProvider clock, ILogger<LoginLinkService> logger) : ILoginLinkService
{
    public Task<Result<LoginLinkPreviewResponse>> PreviewAsync(PreviewLoginLinkRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<LoginLinkPreviewResponse>(logger, clock, "PreviewLoginLink", () =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<Result<LoginLinkPreviewResponse>>(ContractErrors.NotImplemented);
        });

    public Task<Result<LoginLinkRedeemResponse>> RedeemAsync(RedeemLoginLinkRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<LoginLinkRedeemResponse>(logger, clock, "RedeemLoginLink", () =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<Result<LoginLinkRedeemResponse>>(ContractErrors.NotImplemented);
        });
}
