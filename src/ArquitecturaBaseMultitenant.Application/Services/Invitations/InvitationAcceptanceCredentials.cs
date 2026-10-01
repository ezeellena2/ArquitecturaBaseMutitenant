using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Identity;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationAcceptanceCredentials(ILoginMethodReader methods,
    LoginMethodAvailability availability, IInvitationTokenProtector tokens)
{
    internal async Task<bool> HasPersonalBackupAsync(Guid userId, CancellationToken ct) =>
        await availability.HasPersonalBackupAsync(userId, await methods.ListByUserIdAsync(userId, ct), ct);

    internal string HashNonce(string nonce) => tokens.Hash(nonce);
}
