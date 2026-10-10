using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// «Recuperar mi cuenta» (ADR 0043). Consulta la cuenta anterior sin escribir; cada paso que escribe abre su UoW.
/// Sólo emite para un destino nuevo libre y registra el pedido una vez por código, revalidando la propiedad.
/// </summary>
internal sealed class AccountRecoveryService(
    LoginCodeIssuer issuer,
    LoginCodeVerifier verifier,
    ISecureTokenGenerator secrets,
    AccountRecoveryGuard guard,
    AccountRecoveryNotifier messenger,
    IAccountRecoveryRepository recoveries,
    LoginDestinationResolver destinations,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AccountRecoveryService> logger) : IAccountRecoveryService
{
    public Task<Result> PreviewAsync(PreviewAccountRecoveryRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "PreviewAccountRecovery", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var previous = guard.Previous(request.PreviousEmail, request.PreviousPhone);
            if (previous.IsFailure) return previous.Error;
            return await guard.FindPreviousAsync(previous.Value, cancellationToken) is null
                ? AccountRecoveryGuard.PreviousNotFound(previous.Value.Type)
                : Result.Success();
        });

    public Task<Result<RequestLoginCodeResponse>> RequestCodeAsync(RequestRecoveryCodeRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RequestLoginCodeResponse>(logger, timeProvider, "RequestRecoveryCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var resolved = destinations.Resolve(request.Email, request.Phone, request.Channel);
            if (resolved.IsFailure) return resolved.Error;
            var destination = resolved.Value;
            var nonceHash = secrets.Hash(request.Nonce!);
            return await unitOfWork.ExecuteInTransactionAsync<Result<RequestLoginCodeResponse>>(async ct =>
            {
                await guard.LockAsync(destination, null, ct);
                var valid = await guard.CheckNewAsync(destination, ct);
                if (valid.IsFailure) return valid.Error;
                var issued = await issuer.IssueRecoveryCodeAsync(destination, nonceHash, ct);
                if (issued.IsFailure) return issued.Error;
                var admitted = await issuer.AdmitAsync(destination, issued.Value, ct);
                if (admitted.IsFailure) return admitted.Error;
                var sent = await messenger.SendCodeAsync(destination, issued.Value, ct);
                if (sent.IsFailure) return sent.Error;
                return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> VerifyCodeAsync(VerifyRecoveryCodeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifyRecoveryCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var resolved = destinations.Resolve(request.Email, request.Phone, request.Channel);
            if (resolved.IsFailure) return resolved.Error;
            var destination = resolved.Value;
            var nonceHash = secrets.Hash(request.Nonce!);
            // Los intentos fallidos se guardan aunque la verificación falle.
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await guard.LockAsync(destination, null, ct);
                if (LoginDestinationResolver.Type(destination) == LoginMethodType.Phone)
                {
                    var valid = await guard.CheckNewAsync(destination, ct);
                    if (valid.IsFailure) return valid;
                }
                return await verifier.VerifyRecoveryAsync(destination, nonceHash, request.Code!, ct);
            }, CommitPolicy.OnAnyResult, cancellationToken);
        });

    public Task<Result> SubmitAsync(SubmitAccountRecoveryRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "SubmitAccountRecovery", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var previous = guard.Previous(request.PreviousEmail,
                string.IsNullOrWhiteSpace(request.PreviousPhoneNumber) ? null : new PhoneInputRequest(request.PreviousPhoneCountry, request.PreviousPhoneNumber));
            if (previous.IsFailure) return previous.Error;
            var resolved = destinations.Resolve(request.Email, request.Phone, request.Channel);
            if (resolved.IsFailure) return resolved.Error;
            var destination = resolved.Value;
            if (previous.Value.Type == LoginDestinationResolver.Type(destination) && previous.Value.Value == destination.Value)
                return new ValidationError(new Dictionary<string, string[]>
                {
                    [previous.Value.Type == LoginMethodType.Phone ? "previousPhone" : "previousEmail"] =
                        [previous.Value.Type == LoginMethodType.Phone ? ValidationTexts.RecoveryPhoneRepeated : ValidationTexts.RecoveryMethodRepeated],
                });
            var nonceHash = secrets.Hash(request.Nonce!);
            return await unitOfWork.ExecuteInTransactionAsync<Result>(async ct =>
            {
                var userId = await guard.FindPreviousAsync(previous.Value, ct);
                await guard.LockAsync(destination, userId, ct);
                var code = await verifier.FindVerifiedRecoveryAsync(destination, nonceHash, ct);
                if (code is null || await recoveries.ExistsForLoginCodeAsync(code.Id, ct))
                    return AccountRecoveryErrors.VerificationRequired;
                var (type, value) = previous.Value;
                if (userId is null || await guard.FindPreviousAsync(previous.Value, ct) != userId)
                    return AccountRecoveryGuard.PreviousNotFound(type);
                var valid = await guard.CheckNewAsync(destination, ct);
                if (valid.IsFailure) return valid.Error;
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                recoveries.Add(LoginDestinationResolver.Type(destination) == LoginMethodType.Email
                    ? AccountRecoveryRequest.Submit(code.Id, type, value, request.Email!, userId, nowUtc)
                    : AccountRecoveryRequest.Submit(code.Id, type, value,
                        Domain.ValueObjects.PhoneNumber.Create(destination.Value).Value, userId, nowUtc));
                // El aviso a la cuenta encontrada sale después, con AccountRecoveryNoticeService.
                await messenger.NotifyReceivedAsync(destination, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
}
