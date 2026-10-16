using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Contrato E9 publicado antes de implementarlo (K1): responde 501 sin consumir códigos.</summary>
internal sealed class PlatformReauthenticationService(TimeProvider clock, ILogger<PlatformReauthenticationService> logger)
    : IPlatformReauthenticationService
{
    public Task<Result<PlatformReauthenticationResponse>> ReauthenticateAsync(PlatformReauthenticationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformReauthenticate", () => Task.FromResult(Result.Failure<PlatformReauthenticationResponse>(ContractErrors.NotImplemented)));
}
