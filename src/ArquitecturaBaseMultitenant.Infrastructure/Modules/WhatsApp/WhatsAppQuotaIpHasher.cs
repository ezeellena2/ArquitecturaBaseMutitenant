using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>Reutiliza la clave HMAC validada del OTP con dominio independiente; nunca almacena la IP original.</summary>
internal sealed class WhatsAppQuotaIpHasher(IOptions<LoginCodeHashOptions> options) : IWhatsAppQuotaIpHasher
{
    private readonly byte[] _key = Convert.FromBase64String(options.Value.HashKey);
    public string Hash(string? ipAddress, string phoneNumberId, DateOnly dayUtc)
    {
        // Una IPv6 cuenta por su /64 (N20).
        var canonical = IpAddressKey.Normalize(ipAddress);
        var message = JsonSerializer.SerializeToUtf8Bytes(new object[] { "whatsapp-code-quota-ip:v1", phoneNumberId, dayUtc.DayNumber, canonical });
        return Convert.ToBase64String(HMACSHA256.HashData(_key, message));
    }
}
