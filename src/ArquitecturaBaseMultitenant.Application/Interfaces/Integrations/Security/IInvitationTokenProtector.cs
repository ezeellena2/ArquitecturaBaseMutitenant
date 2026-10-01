using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

public interface IInvitationTokenProtector
{
    string GenerateSecret();
    string Hash(string secret);
    string Protect(InvitationTokenData data);
    InvitationTokenData? Unprotect(string token);
}
