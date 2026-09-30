using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

internal sealed class AccountDeletionService(ICurrentUser currentUser, AccountDeletionRequester requester,
    IRequestValidator validator, IUnitOfWork unitOfWork, AccountDeletionSessionRestorer sessions,
    AccountDeletionCanceller canceller,
    TimeProvider timeProvider, ILogger<AccountDeletionService> logger) : IAccountDeletionService
{
    public Task<Result<AccountDeletionResponse>> RequestAsync(RequestAccountDeletionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<AccountDeletionResponse>(logger, timeProvider, "RequestAccountDeletion", async () =>
        {
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            var result = await unitOfWork.ExecuteInTransactionAsync(
                innerCt => requester.RequestAsync(userId, request, innerCt), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await sessions.InvalidateAsync(userId, ct);
            return result;
        });

    public Task<Result<CancelAccountDeletionResponse>> CancelAsync(CancelAccountDeletionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<CancelAccountDeletionResponse>(logger, timeProvider, "CancelAccountDeletion", async () =>
        {
            var result = await unitOfWork.ExecuteInTransactionAsync(
                innerCt => canceller.CancelAsync(request, innerCt), CommitPolicy.OnSuccess, ct);
            if (result.IsSuccess) await sessions.RestoreAsync(result.Value.UserId, ct);
            return result;
        });
}
