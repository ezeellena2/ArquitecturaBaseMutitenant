using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Valida la identidad y ejecuta el alta empresarial bajo el lock y la transacción idempotente.</summary>
internal sealed class BusinessSignupTransactionRunner(
    ITenantScope tenantScope,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IUserRepository users,
    ILoginMethodRepository loginMethods)
{
    internal Result EnsureNoActiveTenant() =>
        tenantContext.TenantId is null ? Result.Success() : BusinessSignupErrors.ActiveSpace;

    internal async Task<Result<BusinessSignupResponse>> ExecuteAsync(Guid userId, Guid operationId,
        Func<UserAccountRow, CancellationToken, Task<Result<BusinessSignupResponse>>> execute,
        CancellationToken cancellationToken)
    {
        using var scope = tenantScope.Enter(operationId);
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await loginMethods.LockUserAsync(userId, ct);
                var account = await users.GetByIdAsync(userId, ct);
                if (account is null || account.Status == UserStatus.Deleted)
                    return BusinessSignupErrors.IdentityRequired;
                if (account.Status == UserStatus.Suspended) return AccountErrors.Suspended;
                if (account.Status == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
                return await execute(account, ct);
            }, CommitPolicy.OnSuccess, cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
            when (exception.ConstraintName == "IX_Tenants_Slug")
        {
            return TenantSlugErrors.Taken;
        }
    }
}
