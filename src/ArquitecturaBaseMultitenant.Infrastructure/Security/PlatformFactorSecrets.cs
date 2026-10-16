using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>
/// El material del segundo factor de los operadores (E9). La clave TOTP (RFC 6238: HMAC-SHA1, 30 segundos y seis
/// dígitos, con un intervalo de tolerancia por lado) se guarda protegida con Data Protection y ligada a la cuenta; las
/// réplicas comparten el anillo de claves. Los códigos de recuperación se guardan con HMAC-SHA256 y la misma clave
/// secreta de los códigos de ingreso, ligados a la cuenta; mientras el alta no termina viajan protegidos en el desafío.
/// </summary>
internal sealed class PlatformFactorSecrets(IDataProtectionProvider provider, IOptions<LoginCodeHashOptions> hashOptions,
    IOptions<EmailOptions> emailOptions) : IPlatformFactorSecrets
{
    private const string SecretPurpose = "ArquitecturaBaseMultitenant.PlatformFactor.Secret.v1";
    private const string RecoveryCodesPurpose = "ArquitecturaBaseMultitenant.PlatformFactor.RecoveryCodes.v1";
    private const string SessionPurpose = "ArquitecturaBaseMultitenant.PlatformFactor.Session.v1";
    private const int SecretBytes = 20;
    private const int StepSeconds = 30;
    private const int RecoveryCodeCount = PlatformFactorChallengeResponse.RecoveryCodeCount;
    // Sin 0/O ni 1/I: la persona los copia a mano.
    private const string RecoveryAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private readonly byte[] _hashKey = Convert.FromBase64String(hashOptions.Value.HashKey);

    public byte[] NewProtectedSecret(Guid userId)
    {
        var secret = RandomNumberGenerator.GetBytes(SecretBytes);
        try
        {
            return Protector(SecretPurpose, userId).Protect(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public PlatformAuthenticatorSetup DescribeSetup(Guid userId, byte[] protectedSecret, string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        var secret = Protector(SecretPurpose, userId).Unprotect(protectedSecret);
        try
        {
            var encoded = Base32(secret);
            var issuer = emailOptions.Value.AppName;
            var label = Uri.EscapeDataString(issuer + ":" + accountName);
            return new PlatformAuthenticatorSetup(encoded,
                $"otpauth://totp/{label}?secret={encoded}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={TwoFactorCodeFormat.AuthenticatorDigits}&period={StepSeconds}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public long? MatchTimeStep(Guid userId, byte[] protectedSecret, string code, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        if (!TwoFactorCodeFormat.IsAuthenticatorCode(code)) return null;
        var secret = Protector(SecretPurpose, userId).Unprotect(protectedSecret);
        try
        {
            var current = new DateTimeOffset(nowUtc).ToUnixTimeSeconds() / StepSeconds;
            long? matched = null;
            // Se comparan los tres intervalos siempre, en tiempo constante, para no revelar cuál coincidió.
            for (var offset = -1; offset <= 1; offset++)
            {
                var step = current + offset;
                if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Totp(secret, step)),
                        Encoding.ASCII.GetBytes(code)))
                    matched ??= step;
            }
            return matched;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public IReadOnlyList<string> NewRecoveryCodes()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        while (codes.Count < RecoveryCodeCount)
        {
            var compact = RandomNumberGenerator.GetString(RecoveryAlphabet, TwoFactorCodeFormat.RecoveryGroupLength * 2);
            codes.Add(string.Concat(compact.AsSpan(0, TwoFactorCodeFormat.RecoveryGroupLength), "-",
                compact.AsSpan(TwoFactorCodeFormat.RecoveryGroupLength)));
        }
        return [.. codes];
    }

    public string HashRecoveryCode(Guid userId, string normalizedCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedCode);
        var message = Encoding.UTF8.GetBytes(string.Join('\n', userId.ToString("N"), "PlatformRecoveryCode", normalizedCode));
        return Convert.ToHexString(HMACSHA256.HashData(_hashKey, message));
    }

    public byte[] ProtectRecoveryCodes(Guid challengeId, IReadOnlyList<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        return Protector(RecoveryCodesPurpose, challengeId).Protect(Encoding.UTF8.GetBytes(string.Join('\n', codes)));
    }

    public IReadOnlyList<string> UnprotectRecoveryCodes(Guid challengeId, byte[] protectedCodes) =>
        Encoding.UTF8.GetString(Protector(RecoveryCodesPurpose, challengeId).Unprotect(protectedCodes)).Split('\n');

    public byte[] ProtectSessionId(Guid platformSessionId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return Protector(SessionPurpose, platformSessionId).Protect(Encoding.UTF8.GetBytes(sessionId));
    }

    public string UnprotectSessionId(Guid platformSessionId, byte[] protectedSessionId) =>
        Encoding.UTF8.GetString(Protector(SessionPurpose, platformSessionId).Unprotect(protectedSessionId));

    /// <summary>RFC 6238 sobre RFC 4226: HOTP del intervalo con HMAC-SHA1 y truncado dinámico a seis dígitos.</summary>
    internal static string Totp(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
#pragma warning disable CA5350 // RFC 6238 y las apps autenticadoras usan HMAC-SHA1; no es un hash de contraseñas.
        HMACSHA1.HashData(secret, counter, hash);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7FFFFFFF) % 1_000_000;
        // Seis dígitos con ceros a la izquierda; es un código, no un número para mostrar.
        return string.Create(TwoFactorCodeFormat.AuthenticatorDigits, binary, static (digits, value) =>
        {
            for (var index = digits.Length - 1; index >= 0; index--)
            {
                digits[index] = (char)('0' + value % 10);
                value /= 10;
            }
        });
    }

    /// <summary>RFC 4648 base32 sin relleno, el formato que leen las apps autenticadoras.</summary>
    internal static string Base32(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var value in data)
        {
            buffer = ((buffer << 8) | value) & 0xFFFF;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(Base32Alphabet[(buffer >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }
        if (bits > 0) builder.Append(Base32Alphabet[(buffer << (5 - bits)) & 0x1F]);
        return builder.ToString();
    }

    private IDataProtector Protector(string purpose, Guid scope) => provider.CreateProtector(purpose, scope.ToString("N"));
}
