using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>
/// La partición de una IP para las cuotas (E9): reutiliza la clave HMAC validada del código de ingreso, compartida entre
/// réplicas, con un dominio propio. Nunca guarda la IP.
/// </summary>
internal sealed class RateLimitIpHasher(IOptions<LoginCodeHashOptions> options) : IRateLimitIpHasher
{
    private readonly byte[] _key = Convert.FromBase64String(options.Value.HashKey);

    public string Hash(string? ipAddress)
    {
        // Una IPv6 cuenta por su /64 (N20).
        var message = JsonSerializer.SerializeToUtf8Bytes(new object[] { "rate-limit-ip:v1", IpAddressKey.Normalize(ipAddress) });
        return Convert.ToHexStringLower(HMACSHA256.HashData(_key, message).AsSpan(0, 16));
    }
}
