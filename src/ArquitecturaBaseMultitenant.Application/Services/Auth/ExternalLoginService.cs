using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Confirma el callback Google y emite la cookie solo después del commit. Un operador que entra a Plataforma recibe en
/// cambio la cookie del desafío pendiente (E9): la sesión nace recién con el segundo factor.
/// </summary>
internal sealed class ExternalLoginService(
    ISignInService signIn,
    GoogleAccountResolver accounts,
    ITenantScope tenantScope,
    ILoginAuditRepository audits,
    IUserRepository users,
    PlatformFactorChallengeIssuer factors,
    IPlatformFactorBrowser browser,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ExternalLoginService> logger) : IExternalLoginService
{
    public Task<Result> ValidateStartAsync(ExternalLoginStartRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "ExternalLoginStart", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return await validator.ValidateAsync(request, cancellationToken) is { } invalid
                ? Result.Failure(invalid) : Result.Success();
        });

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

            // Se resuelven los valores de referencia antes de la transacción; solo el alta Personal entra a ese scope.
            var draft = request.IsSignup && login is not null
                ? await accounts.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken)
                : null;
            using var scope = draft is null || request.RequestedAccess == Access.Business
                ? null : tenantScope.Enter(draft.Tenant.Id);
            var access = ReturnUrls.TryReadAccessSelection(request.ReturnUrl, out var selection)
                ? selection.Access : Access.Business;
            var result = await unitOfWork.ExecuteInTransactionAsync(
                ct => SignInCoreAsync(request, login, draft, access, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (result.IsFailure) return result.Error;

            if (result.Value.Factor is { } factor)
            {
                browser.Write(factor);
                return new ExternalSignInResponse(request.ReturnUrl!, PlatformFactorPending: true);
            }
            await signIn.SignInAsync(result.Value.UserId, access, cancellationToken);
            return new ExternalSignInResponse(request.ReturnUrl!);
        });

    private async Task<Result<GoogleSignIn>> SignInCoreAsync(ExternalSignInRequest request, ExternalLogin? login,
        PersonalSpaceDraft? draft, Access access, CancellationToken cancellationToken)
    {
        var resolved = await accounts.ResolveAsync(request, login, draft, cancellationToken);
        if (resolved.IsFailure) return Fail(null, resolved.Error);

        var userId = resolved.Value;
        if (await signIn.IsLockedOutAsync(userId, cancellationToken))
            return Fail(userId, AccountErrors.LockedOut);

        if (access == Access.Platform && await users.GetByIdAsync(userId, cancellationToken) is { IsPlatformOperator: true })
        {
            // E9 (ADR 0043): Google probó el primer factor. La sesión, el borrado de los fallos y el ingreso exitoso
            // esperan al segundo; el desafío nace en esta misma transacción.
            return new GoogleSignIn(userId, await factors.IssueAsync(userId, request.ReturnUrl!, cancellationToken));
        }

        await signIn.ResetFailedAttemptsAsync(userId, cancellationToken);
        audits.Add(LoginAudit.Success(userId, LoginAuditMethod.Google, timeProvider.GetUtcNow().UtcDateTime));
        return new GoogleSignIn(userId, null);
    }

    private sealed record GoogleSignIn(Guid UserId, PlatformFactorBinding? Factor);

    private Error Fail(Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, LoginAuditMethod.Google, error,
            timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }
}
