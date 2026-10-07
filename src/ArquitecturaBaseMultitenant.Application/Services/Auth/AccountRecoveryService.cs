using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// «Recuperar mi cuenta» (ADR 0043). Consulta la cuenta anterior sin escribir; cada paso que escribe abre su UoW.
/// Sólo emite para un correo nuevo libre y registra el pedido una vez por código, revalidando la propiedad.
/// </summary>
internal sealed class AccountRecoveryService(
    LoginCodeIssuer issuer,
    LoginCodeVerifier verifier,
    ISecureTokenGenerator secrets,
    IUserLookup userLookup,
    AccountRecoveryNotifier messenger,
    IAccountRecoveryRepository recoveries,
    IPhoneNumberParser phones,
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
            return await userLookup.FindVerifiedUserIdAsync(LoginMethodType.Email, request.PreviousEmail!.Value,
                cancellationToken) is null
                ? FieldErrors.On(AccountRecoveryErrors.AccountNotFound, "previousEmail")
                : Result.Success();
        });

    public Task<Result<RequestLoginCodeResponse>> RequestCodeAsync(RequestRecoveryCodeRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RequestLoginCodeResponse>(logger, timeProvider, "RequestRecoveryCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var destination = LoginCodeDestination.ForEmail(request.Email!);
            var nonceHash = secrets.Hash(request.Nonce!);
            return await unitOfWork.ExecuteInTransactionAsync<Result<RequestLoginCodeResponse>>(async ct =>
            {
                await issuer.LockDestinationAsync(destination, ct);
                var owners = await userLookup.FindVerifiedUsersByEmailAsync(request.Email!, ct);
                if (owners.Count > 0) return FieldErrors.On(AccountRecoveryErrors.MethodInUse, "email");
                var issued = await issuer.IssueRecoveryCodeAsync(destination, nonceHash, ct);
                if (issued.IsFailure) return issued.Error;
                await messenger.SendCodeAsync(destination, issued.Value, ct);
                return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> VerifyCodeAsync(VerifyRecoveryCodeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifyRecoveryCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var destination = LoginCodeDestination.ForEmail(request.Email!);
            var nonceHash = secrets.Hash(request.Nonce!);
            // Los intentos fallidos se guardan aunque la verificación falle.
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => verifier.VerifyRecoveryAsync(destination, nonceHash, request.Code!, ct),
                CommitPolicy.OnAnyResult, cancellationToken);
        });

    public Task<Result> SubmitAsync(SubmitAccountRecoveryRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "SubmitAccountRecovery", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var previous = PreviousMethod(request);
            if (previous.IsFailure) return previous.Error;
            var newEmail = request.Email!;
            var destination = LoginCodeDestination.ForEmail(newEmail);
            var nonceHash = secrets.Hash(request.Nonce!);
            return await unitOfWork.ExecuteInTransactionAsync<Result>(async ct =>
            {
                var code = await verifier.FindVerifiedRecoveryAsync(destination, nonceHash, ct);
                if (code is null || await recoveries.ExistsForLoginCodeAsync(code.Id, ct))
                    return AccountRecoveryErrors.VerificationRequired;
                var (type, value) = previous.Value;
                var userId = await userLookup.FindVerifiedUserIdAsync(type, value, ct);
                if (type == LoginMethodType.Email && userId is null)
                    return FieldErrors.On(AccountRecoveryErrors.AccountNotFound, "previousEmail");
                if ((await userLookup.FindVerifiedUsersByEmailAsync(newEmail, ct)).Count > 0)
                    return FieldErrors.On(AccountRecoveryErrors.MethodInUse, "email");
                recoveries.Add(AccountRecoveryRequest.Submit(code.Id, type, value, newEmail, userId,
                    timeProvider.GetUtcNow().UtcDateTime));
                // El aviso a la cuenta encontrada sale después, con AccountRecoveryNoticeService.
                await messenger.NotifyReceivedAsync(destination.Value, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });


    private Result<(LoginMethodType Type, string Value)> PreviousMethod(SubmitAccountRecoveryRequest request)
    {
        if (request.PreviousEmail is { } email) return Result.Success((LoginMethodType.Email, email.Value));
        var phone = phones.Parse(request.PreviousPhoneCountry, request.PreviousPhoneNumber, PhoneUsage.Mobile);
        return phone.IsFailure
            ? FieldErrors.On(phone.Error, "previousPhone")
            : Result.Success((LoginMethodType.Phone, phone.Value.Value));
    }
}
