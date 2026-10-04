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
/// «Recuperar mi cuenta» (multitenancy.md §3.1, ADR 0041). Cada paso abre su UoW: el código se emite igual exista o
/// no la cuenta, la verificación guarda los intentos fallidos y el pedido se registra una sola vez por código.
/// </summary>
internal sealed class AccountRecoveryService(
    LoginCodeIssuer issuer,
    LoginCodeVerifier verifier,
    ISecureTokenGenerator secrets,
    IUserLookup userLookup,
    AccountRecoveryMessenger messenger,
    IAccountRecoveryRepository recoveries,
    IPhoneNumberParser phones,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AccountRecoveryService> logger) : IAccountRecoveryService
{
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
                var issued = await issuer.IssueRecoveryCodeAsync(destination, nonceHash, ct);
                if (issued.IsFailure) return issued.Error;
                // Si el correo ya es de una cuenta (un método de correo o el contacto de Google, verificados), a su
                // dueño le llega un aviso en lugar del código. El código se emite igual, sin mandarlo: los límites, los
                // intentos y la respuesta no cambian.
                var owners = await userLookup.FindVerifiedUsersByEmailAsync(request.Email!, ct);
                if (owners.Count > 0)
                    await messenger.NotifyMethodInUseAsync(owners[0], destination.Value, ct);
                else
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
                recoveries.Add(AccountRecoveryRequest.Submit(code.Id, type, value, newEmail, userId,
                    timeProvider.GetUtcNow().UtcDateTime));
                // El aviso a la cuenta encontrada sale después, con AccountRecoveryNoticeService: así el pedido
                // escribe lo mismo y tarda lo mismo exista o no la cuenta (ADR 0041).
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
