using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>
/// La clave que ordena un destino en la cola (ADR 0066): un HMAC con la clave validada del código de ingreso, compartida entre
/// réplicas, con un dominio propio y el canal adentro. La cola nunca guarda el destino en claro.
/// </summary>
internal sealed class OutboxOrderingKeyHasher(IOptions<LoginCodeHashOptions> options)
{
    private readonly byte[] _key = Convert.FromBase64String(options.Value.HashKey);

    public string Hash(string channel, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var message = JsonSerializer.SerializeToUtf8Bytes(new object[] { "outbox-ordering:v1", channel, destination });
        return Convert.ToHexStringLower(HMACSHA256.HashData(_key, message));
    }
}
