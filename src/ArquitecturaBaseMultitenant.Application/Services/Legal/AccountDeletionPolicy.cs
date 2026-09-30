using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Impide pedir la baja cuando la cuenta es operadora, ya está pendiente o no está activa.</summary>
internal static class AccountDeletionPolicy
{
    internal static Error? CheckRequest(UserAccountRow account)
    {
        if (account.IsPlatformOperator) return AccountDeletionErrors.PlatformOperator;
        if (account.DeletionScheduledForUtc is not null || account.Status == UserStatus.PendingDeletion)
            return AccountDeletionErrors.AlreadyPending;
        return account.Status == UserStatus.Active ? null : AccountErrors.Suspended;
    }
}
