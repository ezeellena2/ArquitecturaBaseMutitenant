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

internal sealed class LegalAcceptanceService(ICurrentUser currentUser, LegalAcceptanceWriter writer, LegalAcceptanceGuard guard,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    ILogger<LegalAcceptanceService> logger) : ILegalAcceptanceService
{
    public Task<Result<IReadOnlyList<PendingLegalDocumentResponse>>> GetPendingAsync(CancellationToken ct) =>
        OperationLog.RunAsync<IReadOnlyList<PendingLegalDocumentResponse>>(logger, timeProvider,
            "GetPendingLegalDocuments", async () => currentUser.UserId is { } userId
                ? Result.Success(await guard.PendingAsync(userId, ct)) : UserErrors.NotFound);

    public Task<Result> AcceptAsync(AcceptLegalRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "AcceptLegalDocuments", async () =>
        {
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(
                innerCt => writer.AcceptAsync(userId, request, innerCt), CommitPolicy.OnSuccess, ct);
        });
}
