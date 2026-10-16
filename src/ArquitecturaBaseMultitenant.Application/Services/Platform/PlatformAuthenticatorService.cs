using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Contrato E9 publicado antes de implementarlo (K1): responde 501 sin tocar al operador.</summary>
internal sealed class PlatformAuthenticatorService(TimeProvider clock, ILogger<PlatformAuthenticatorService> logger)
    : IPlatformAuthenticatorService
{
    public Task<Result<VersionResponse>> ResetAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformAuthenticatorReset", () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));
}
