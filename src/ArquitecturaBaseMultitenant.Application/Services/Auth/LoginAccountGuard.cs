using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Ordena cuenta antes de destino, igual que la gestión de Cuenta, y revalida su dueño.</summary>
internal sealed class LoginAccountGuard(IUserLookup lookup, IUserRepository users, ILoginMethodRepository methods,
    TimeProvider clock)
{
    internal async Task<Guid?> LockAsync(LoginCodeDestination destination, CancellationToken ct)
    {
        var id = await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, destination.Value, ct);
        if (id is { } userId) await methods.LockUserAsync(userId, ct);
        return id;
    }
    internal async Task<bool> StillOwnsAsync(Guid? id, LoginCodeDestination destination, CancellationToken ct) =>
        id == await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, destination.Value, ct);
    internal Task<UserAccountRow?> ReadAsync(Guid id, CancellationToken ct) => users.GetByIdAsync(id, ct);

    /// <summary>
    /// ADR 0042: la invitación de un operador queda pendiente hasta el primer ingreso con código a Plataforma, que la
    /// acepta bajo el lock de la cuenta. Por otro acceso responde <c>PlatformOnly</c> sin sesión; vencida, el código
    /// falla como uno que no sirve.
    /// </summary>
    internal async Task<Result> AcceptOperatorInvitationAsync(UserAccountRow account, string returnUrl,
        CancellationToken ct)
    {
        if (!account.HasPendingOperatorInvitation) return Result.Success();
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (account.OperatorInvitationExpiresAtUtc <= nowUtc) return LoginCodeErrors.Invalid(attemptsLeft: null);
        if (!ReturnUrls.TryReadAccessSelection(returnUrl, out var selection) || selection.Access != Access.Platform)
            return AccessErrors.PlatformOnly;
        return await users.AcceptOperatorInvitationAsync(account.Id, nowUtc, ct);
    }
}
