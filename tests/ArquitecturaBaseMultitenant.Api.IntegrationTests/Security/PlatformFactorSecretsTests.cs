using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Security;

/// <summary>
/// El material del segundo factor (E9): el TOTP sigue los vectores de RFC 6238, la clave del QR es base32 de RFC 4648,
/// la tolerancia es de un intervalo por lado y los códigos de recuperación quedan con hash con clave ligado a la cuenta.
/// </summary>
public sealed class PlatformFactorSecretsTests
{
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Totp_matches_the_rfc_6238_sha1_vectors_truncated_to_six_digits(long unixSeconds, string expected) =>
        Assert.Equal(expected, PlatformFactorSecrets.Totp(RfcSecret, unixSeconds / 30));

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Secret_is_rfc_4648_base32_without_padding(string value, string expected) =>
        Assert.Equal(expected, PlatformFactorSecrets.Base32(Encoding.ASCII.GetBytes(value)));

    [Fact]
    public void Setup_describes_the_same_secret_the_code_is_checked_against_with_one_interval_per_side()
    {
        var secrets = Create();
        var userId = Guid.NewGuid();
        var protectedSecret = secrets.NewProtectedSecret(userId);
        var setup = secrets.DescribeSetup(userId, protectedSecret, "ana@example.test");
        Assert.Matches("^[A-Z2-7]{32}$", setup.Secret);
        Assert.StartsWith("otpauth://totp/Arquitectura%20Base%3Aana%40example.test?secret=" + setup.Secret, setup.OtpauthUri, StringComparison.Ordinal);
        Assert.Contains("&issuer=Arquitectura%20Base&algorithm=SHA1&digits=6&period=30", setup.OtpauthUri, StringComparison.Ordinal);

        var raw = Decode(setup.Secret);
        var nowUtc = new DateTime(2026, 10, 16, 12, 0, 10, DateTimeKind.Utc);
        var step = new DateTimeOffset(nowUtc).ToUnixTimeSeconds() / 30;
        Assert.Equal(step, secrets.MatchTimeStep(userId, protectedSecret, PlatformFactorSecrets.Totp(raw, step), nowUtc));
        Assert.Equal(step - 1, secrets.MatchTimeStep(userId, protectedSecret, PlatformFactorSecrets.Totp(raw, step - 1), nowUtc));
        Assert.Equal(step + 1, secrets.MatchTimeStep(userId, protectedSecret, PlatformFactorSecrets.Totp(raw, step + 1), nowUtc));
        Assert.Null(secrets.MatchTimeStep(userId, protectedSecret, PlatformFactorSecrets.Totp(raw, step + 2), nowUtc));
        Assert.Null(secrets.MatchTimeStep(userId, protectedSecret, "12345", nowUtc));
        // La clave protegida para una cuenta no sirve para otra.
        Assert.ThrowsAny<CryptographicException>(() => secrets.MatchTimeStep(Guid.NewGuid(), protectedSecret, "000000", nowUtc));
    }

    [Fact]
    public void Recovery_codes_are_eight_distinct_readable_codes_with_a_keyed_hash_bound_to_the_account()
    {
        var secrets = Create();
        var codes = secrets.NewRecoveryCodes();
        Assert.Equal(8, codes.Count);
        Assert.Equal(8, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", code));

        var userId = Guid.NewGuid();
        var hash = secrets.HashRecoveryCode(userId, codes[0]);
        Assert.Matches("^[0-9A-F]{64}$", hash);
        Assert.Equal(hash, secrets.HashRecoveryCode(userId, codes[0]));
        Assert.NotEqual(hash, secrets.HashRecoveryCode(Guid.NewGuid(), codes[0]));
        Assert.NotEqual(hash, Create(key: 7).HashRecoveryCode(userId, codes[0]));

        var challengeId = Guid.NewGuid();
        var protectedCodes = secrets.ProtectRecoveryCodes(challengeId, codes);
        Assert.Equal(codes, secrets.UnprotectRecoveryCodes(challengeId, protectedCodes));
        Assert.ThrowsAny<CryptographicException>(() => secrets.UnprotectRecoveryCodes(Guid.NewGuid(), protectedCodes));
    }

    [Theory]
    [InlineData("abcd-2345", "ABCD-2345")]
    [InlineData("ABCD2345", "ABCD-2345")]
    [InlineData("ABC-D2345", null)]
    [InlineData("ABCD--2345", null)]
    [InlineData("ABCD-234", null)]
    [InlineData("ABCD-23 5", null)]
    [InlineData(null, null)]
    public void Recovery_codes_are_read_in_any_case_with_or_without_the_dash(string? typed, string? expected) =>
        Assert.Equal(expected, TwoFactorCodeFormat.NormalizeRecoveryCode(typed));

    private static PlatformFactorSecrets Create(int key = 1) => new(new EphemeralDataProtectionProvider(),
        Options.Create(new LoginCodeHashOptions { HashKey = Convert.ToBase64String(Enumerable.Repeat((byte)key, 32).ToArray()) }),
        Options.Create(new EmailOptions()));

    private static byte[] Decode(string base32)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var character in base32)
        {
            buffer = ((buffer << 5) | Alphabet.IndexOf(character, StringComparison.Ordinal)) & 0xFFFF;
            bits += 5;
            if (bits < 8) continue;
            bytes.Add((byte)(buffer >> (bits - 8)));
            bits -= 8;
        }
        return [.. bytes];
    }
}
