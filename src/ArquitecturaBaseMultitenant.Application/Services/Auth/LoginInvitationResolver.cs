using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Resuelve el alcance antes de abrir la transacción; no concede acceso ni prueba el correo.</summary>
internal sealed class LoginInvitationResolver(ILoginInvitationReader invitations, IUserLookup lookup,
    IUserRepository users, TimeProvider clock)
{
    internal async Task<LoginInvitationsContext?> PrepareAsync(Email email, AccessSelectionRequest? access, CancellationToken ct)
    {
        if (access is null || access.Access == Access.Platform) return null;
        var pending = await invitations.ListPendingAsync(email, clock.GetUtcNow().UtcDateTime, ct);
        if (pending.Count == 0) return null;
        var owners = await lookup.FindVerifiedUsersByEmailAsync(email, ct);
        var ownerId = owners.Count == 1 ? (Guid?)owners[0] : null;
        var account = ownerId is { } owner ? await users.GetByIdAsync(owner, ct) : null;
        if (account is not null && (account.IsPlatformOperator || account.Status != UserStatus.Active)) return null;
        var chosen = pending.FirstOrDefault(row => row.TenantId == account?.LastBusinessTenantId)
            ?? pending.OrderBy(row => row.IssuedAtUtc).ThenBy(row => row.OrganizationName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.TenantId).First();
        return new LoginInvitationsContext(email, ownerId, pending, chosen.TenantId);
    }
}
