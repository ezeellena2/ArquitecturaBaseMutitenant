using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using Microsoft.AspNetCore.DataProtection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>Protege los IDs de alcance junto al secreto de 256 bits con un propósito separado del outbox.</summary>
internal sealed class InvitationTokenProtector(IDataProtectionProvider provider, ISecureTokenGenerator generator)
    : IInvitationTokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("ArquitecturaBaseMultitenant.Invitation.v1");

    public string GenerateSecret() => generator.Generate();
    public string Hash(string secret) => generator.Hash(secret);
    public string Protect(InvitationTokenData data) => _protector.Protect(JsonSerializer.Serialize(data));

    public InvitationTokenData? Unprotect(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return null;
        try
        {
            var data = JsonSerializer.Deserialize<InvitationTokenData>(_protector.Unprotect(token));
            return data is not null && data.TenantId != Guid.Empty && data.InvitationId != Guid.Empty
                && ISecureTokenGenerator.HasTokenFormat(data.Secret) ? data : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
