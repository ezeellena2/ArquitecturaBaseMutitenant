using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>
/// La clave de un contador de los topes de mensajes (ADR 0063): reutiliza la clave HMAC validada del código de ingreso,
/// compartida entre réplicas, con un dominio propio y el alcance adentro, así la misma cuenta no da la misma clave en dos
/// topes. Nunca guarda la dirección, la cuenta ni la organización.
/// </summary>
internal sealed class MessageQuotaKeyHasher(IOptions<LoginCodeHashOptions> options) : IMessageQuotaKeyHasher
{
    private readonly byte[] _key = Convert.FromBase64String(options.Value.HashKey);

    public string ForAddress(string? ipAddress) => Hash(MessageQuotaScope.CodeIp, IpAddressKey.Normalize(ipAddress));

    public string ForSubject(MessageQuotaScope scope, Guid id)
    {
        if (scope is MessageQuotaScope.Global or MessageQuotaScope.CodeIp)
            throw new ArgumentOutOfRangeException(nameof(scope), "The global and the address quotas have no subject.");
        if (id == Guid.Empty) throw new ArgumentException("The subject is required.", nameof(id));
        return Hash(scope, id.ToString("D", CultureInfo.InvariantCulture));
    }

    private string Hash(MessageQuotaScope scope, string value)
    {
        var message = JsonSerializer.SerializeToUtf8Bytes(new object[] { "message-quota:v1", scope.ToString(), value });
        return Convert.ToHexStringLower(HMACSHA256.HashData(_key, message));
    }
}
