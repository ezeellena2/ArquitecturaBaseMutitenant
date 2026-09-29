using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Registro por correo: el código prueba posesión y el alta completa es atómica.</summary>
internal sealed class AccountService(
    SignupPolicy policy,
    LoginCodeIssuer issuer,
    LoginCodeVerifier verifier,
    ILoginCodeRepository codes,
    IUserLookup userLookup,
    IUserRepository users,
    ILoginMethodRepository methods,
    ILoginMethodReader methodReader,
    ILegalRepository legal,
    ILoginAuditRepository audits,
    IEnumerable<ILoginCodeChannel> channels,
    UserCultures userCultures,
    IPersonalSpaceProvisioner personalSpaces,
    ITenantScope tenantScope,
    IRequestInfo requestInfo,
    ISignInService signIn,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AccountService> logger) : IAccountService
{
    public Task<Result<RequestLoginCodeResponse>> RequestSignupCodeAsync(SignupRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RequestLoginCodeResponse>(logger, timeProvider, "RequestSignupCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => RequestCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> VerifySignupAsync(VerifySignupRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifySignup", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;

            // El tenant nuevo debe estar fijado antes de abrir la única transacción.
            // Una cuenta existente no usará el candidato preparado.
            var draft = await personalSpaces.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken);
            using var scope = tenantScope.Enter(draft.Tenant.Id);
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => VerifyCoreAsync(request, draft, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (verified.IsFailure) return verified.Error;

            await signIn.SignInAsync(verified.Value, cancellationToken);
            return Result.Success();
        });

    private async Task<Result<RequestLoginCodeResponse>> RequestCoreAsync(SignupRequest request,
        CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var allowed = await policy.CanRegisterAsync(cancellationToken);
        if (allowed.IsFailure) return allowed.Error;

        var issued = await issuer.IssueSignupCodeAsync(destination, cancellationToken);
        if (issued.IsFailure) return issued.Error;

        var culture = await userCultures.ResolveAsync(null, request.Culture, cancellationToken);
        var channel = channels.SingleOrDefault(value => value.Key == destination.Channel)
            ?? throw new InvalidOperationException("The email signup channel is not registered.");
        channel.EnqueueSignup(destination.Value, issued.Value.Code, issued.Value.LifetimeMinutes, culture);
        issued.Value.LoginCode.MarkSent(issued.Value.IssuedAtUtc);
        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }

    private async Task<Result<Guid>> VerifyCoreAsync(VerifySignupRequest request,
        PersonalSpaceDraft draft, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var method = await userLookup.FindMethodAsync(LoginMethodType.Email,
            destination.Value, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        LegalDocument? terms = null;
        LegalDocument? privacy = null;
        if (method is null)
        {
            var allowed = await policy.CanRegisterAsync(cancellationToken);
            if (allowed.IsFailure) return Fail(null, allowed.Error);
            terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, cancellationToken)
                ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
            privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, cancellationToken)
                ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
        }
        else
        {
            var account = await users.GetByIdAsync(method.UserId, cancellationToken);
            if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
                return Fail(method.UserId, AccountErrors.Suspended);
            if (account.Status == UserStatus.PendingDeletion)
                return Fail(method.UserId, AccountErrors.PendingDeletion);
        }

        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Signup,
            requestedByUserId: null, request.Code!, cancellationToken);
        if (verified.IsFailure) return Fail(method?.UserId, verified.Error);

        Guid userId;
        if (method is null)
        {
            userId = (await users.CreateAsync(null, draft.Culture, draft.TimeZoneId, cancellationToken)).Id;
            var loginMethod = LoginMethod.CreateEmail(userId, request.Email!);
            loginMethod.Verify(nowUtc);
            if (loginMethod.MakePrimary().IsFailure)
                throw new InvalidOperationException("A verified signup method could not become primary.");
            methods.Add(loginMethod);
            await users.SetPrimaryEmailAsync(userId, request.Email!, cancellationToken);
            personalSpaces.Stage(draft, userId);
            legal.AddAcceptance(LegalAcceptance.Create(userId, terms!, nowUtc,
                requestInfo.IpAddress, requestInfo.UserAgent));
            legal.AddAcceptance(LegalAcceptance.Create(userId, privacy!, nowUtc,
                requestInfo.IpAddress, requestInfo.UserAgent));
        }
        else
        {
            userId = method.UserId;
            if (method.VerifiedAtUtc is null)
            {
                var tracked = await methods.GetByIdAsync(method.MethodId, cancellationToken)
                    ?? throw new InvalidOperationException("The login method disappeared during signup.");
                tracked.Verify(nowUtc);
                var existingMethods = await methodReader.ListByUserIdAsync(userId, cancellationToken);
                if (!existingMethods.Any(value => value.IsPrimary))
                {
                    if (tracked.MakePrimary().IsFailure)
                        throw new InvalidOperationException("A verified method could not become primary.");
                    await users.SetPrimaryEmailAsync(userId, request.Email!, cancellationToken);
                }
            }
        }

        audits.Add(LoginAudit.Success(userId, LoginAuditMethod.Code, nowUtc));
        return userId;
    }

    private Error Fail(Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, LoginAuditMethod.Code, error,
            timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }
}
