using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Contrato E9 publicado antes de implementarlo (K1): responde 501 sin cookies ni códigos consumidos.</summary>
internal sealed class PlatformFactorService(TimeProvider clock, ILogger<PlatformFactorService> logger) : IPlatformFactorService
{
    public Task<Result<PlatformFactorChallengeResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformFactorGet", () => Task.FromResult(Result.Failure<PlatformFactorChallengeResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VerifyPlatformFactorResponse>> VerifyAsync(VerifyPlatformFactorRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformFactorVerify", () => Task.FromResult(Result.Failure<VerifyPlatformFactorResponse>(ContractErrors.NotImplemented)));

    public Task<Result<CompletePlatformFactorResponse>> CompleteAsync(CompletePlatformFactorRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformFactorComplete", () => Task.FromResult(Result.Failure<CompletePlatformFactorResponse>(ContractErrors.NotImplemented)));
}
