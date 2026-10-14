using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>
/// Lo que cambia entre una invitación por correo y una por teléfono al buscar a su dueño y al tomar el lock de identidad.
/// Un teléfono verificado es de una sola cuenta; un correo puede estar en más de una (y entonces nadie elige).
/// </summary>
internal static class InvitationIdentityLookup
{
    internal static async Task<IReadOnlyList<Guid>> FindOwnersAsync(this IUserLookup lookup, InvitationIdentity destination, CancellationToken ct) =>
        destination.Email is { } email ? await lookup.FindVerifiedUsersByEmailAsync(email, ct)
            : await lookup.FindVerifiedUserIdAsync(LoginMethodType.Phone, destination.Value, ct) is { } owner ? [owner] : [];

    internal static Task LockIdentityAsync(this IExternalLoginLock locks, InvitationIdentity destination, CancellationToken ct) =>
        destination.Email is { } email ? locks.LockEmailAsync(email, ct) : locks.LockPhoneAsync(destination.Phone!, ct);
}
