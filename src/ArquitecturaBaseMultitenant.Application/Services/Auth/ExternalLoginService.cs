using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Confirma el callback Google y emite la cookie solo después del commit.</summary>
internal sealed class ExternalLoginService(
    ISignInService signIn,
    GoogleAccountResolver accounts,
    ITenantScope tenantScope,
    ILoginAuditRepository audits,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ExternalLoginService> logger) : IExternalLoginService
{
    public Task<Result<ExternalSignInResponse>> SignInAsync(
        ExternalSignInRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<ExternalSignInResponse>(logger, timeProvider, "ExternalSignIn", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;

            var login = await signIn.GetExternalLoginAsync(cancellationToken);
            if (login is not null)
            {
                await signIn.SignOutExternalAsync(cancellationToken);
            }

            // El candidato Personal y su scope preceden al límite, igual que en AccountService.
            var draft = request.IsSignup && login is not null
                ? await accounts.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken)
                : null;
            using var scope = draft is null ? null : tenantScope.Enter(draft.Tenant.Id);
            var result = await unitOfWork.ExecuteInTransactionAsync(
                ct => SignInCoreAsync(request, login, draft, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (result.IsFailure) return result.Error;

            await signIn.SignInAsync(result.Value, cancellationToken);
            return new ExternalSignInResponse(request.ReturnUrl!);
        });

    private async Task<Result<Guid>> SignInCoreAsync(ExternalSignInRequest request, ExternalLogin? login,
        PersonalSpaceDraft? draft, CancellationToken cancellationToken)
    {
        var resolved = await accounts.ResolveAsync(request, login, draft, cancellationToken);
        if (resolved.IsFailure) return Fail(null, resolved.Error);

        var userId = resolved.Value;
        if (await signIn.IsLockedOutAsync(userId, cancellationToken))
            return Fail(userId, AccountErrors.LockedOut);

        await signIn.ResetFailedAttemptsAsync(userId, cancellationToken);
        audits.Add(LoginAudit.Success(userId, LoginAuditMethod.Google, timeProvider.GetUtcNow().UtcDateTime));
        return userId;
    }

    private Error Fail(Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, LoginAuditMethod.Google, error,
            timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }
}
