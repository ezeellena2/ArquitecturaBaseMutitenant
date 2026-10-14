using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using Microsoft.AspNetCore.DataProtection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>Como el protector de claves del asistente, con un propósito por número: un cifrado no sirve para otro número.</summary>
internal sealed class WhatsAppTokenProtector(IDataProtectionProvider provider) : IWhatsAppTokenProtector
{
    private const string Purpose = "ArquitecturaBaseMultitenant.WhatsApp.ChannelSecret.v1";

    public byte[] Protect(Guid channelId, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        var bytes = Encoding.UTF8.GetBytes(secret);
        try { return Protector(channelId).Protect(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public string Unprotect(Guid channelId, byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        var bytes = Protector(channelId).Unprotect(ciphertext);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private IDataProtector Protector(Guid channelId)
    {
        if (channelId == Guid.Empty) throw new ArgumentException("A channel is required.", nameof(channelId));
        return provider.CreateProtector(Purpose, channelId.ToString("N"));
    }
}
