using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Abre la transacción para pedir o verificar reautenticación; delega las reglas de emisión y comprobación a sus helpers.</summary>
internal sealed class ReauthService(ICurrentUser currentUser, ReauthIssuer issuer, ReauthVerifier verifier,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider timeProvider, ILogger<ReauthService> logger) : IReauthService
{
    public Task<Result<ReauthCodeResponse>> RequestAsync(RequestReauthRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<ReauthCodeResponse>(logger, timeProvider, "RequestAccountReauth", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(token => issuer.RequestAsync(userId, request, token), CommitPolicy.OnSuccess, ct);
        });

    public Task<Result<ReauthResponse>> VerifyAsync(VerifyReauthRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<ReauthResponse>(logger, timeProvider, "VerifyAccountReauth", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(token => verifier.VerifyAsync(userId, request, token), CommitPolicy.OnAnyResult, ct);
        });
}
