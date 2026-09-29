using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Valida la cuenta existente y confirma su método de correo sin duplicar identidad.</summary>
internal sealed class SignupExistingMethodVerifier(IUserRepository users,
    ILoginMethodRepository methods, ILoginMethodReader methodReader)
{
    internal async Task<Error?> CheckAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return AccountErrors.Suspended;
        return account.Status == UserStatus.PendingDeletion ? AccountErrors.PendingDeletion : null;
    }

    internal async Task<Guid> ConfirmAsync(LoginMethodLookup method, Email email, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var userId = method.UserId;
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
                await users.SetPrimaryEmailAsync(userId, email, cancellationToken);
            }
        }

        return userId;
    }
}
