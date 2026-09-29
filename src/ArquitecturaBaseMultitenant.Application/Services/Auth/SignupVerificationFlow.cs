using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Fija el nuevo tenant antes de la única transacción y abre sesión tras el commit.</summary>
internal sealed class SignupVerificationFlow(IPersonalSpaceProvisioner personalSpaces,
    ITenantScope tenantScope, IUnitOfWork unitOfWork, ISignInService signIn,
    SignupVerificationCore core)
{
    internal async Task<Result> VerifyAsync(VerifySignupRequest request,
        CancellationToken cancellationToken)
    {
        // La cuenta existente no usa el candidato, pero comparte la política de scope.
        var draft = await personalSpaces.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken);
        using var scope = tenantScope.Enter(draft.Tenant.Id);
        var verified = await unitOfWork.ExecuteInTransactionAsync(
            ct => core.VerifyAsync(request, draft, ct), CommitPolicy.OnAnyResult, cancellationToken);
        if (verified.IsFailure) return verified.Error;

        await signIn.SignInAsync(verified.Value, cancellationToken);
        return Result.Success();
    }
}
