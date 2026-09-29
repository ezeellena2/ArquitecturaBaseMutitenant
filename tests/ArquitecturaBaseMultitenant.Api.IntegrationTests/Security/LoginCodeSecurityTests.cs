using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Security;

public sealed class LoginCodeSecurityTests
{
    private const string Code = "123456";
    private static readonly LoginCodeDestination Ana = LoginCodeDestination.ForEmail(Email.Create("ana@example.com").Value);
    private static readonly LoginCodeDestination Beto = LoginCodeDestination.ForEmail(Email.Create("beto@example.com").Value);

    [Fact]
    public void Codes_are_random_numeric_and_have_configured_length()
    {
        var generator = new LoginCodeGenerator(Options.Create(new LoginCodeOptions { Length = 8 }));
        var codes = Enumerable.Range(0, 100).Select(_ => generator.Generate()).ToArray();

        Assert.All(codes, code => Assert.Matches("^[0-9]{8}$", code));
        Assert.True(codes.Distinct(StringComparer.Ordinal).Count() > 95);
    }

    [Fact]
    public void Code_hash_is_keyed_and_bound_to_destination_and_purpose()
    {
        var key = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var hasher = new LoginCodeHasher(Options.Create(new LoginCodeHashOptions { HashKey = key }));
        var hash = hasher.Hash(Ana, LoginCodePurpose.Login, Code);

        Assert.Matches("^[0-9A-F]{64}$", hash);
        Assert.Equal(hash, hasher.Hash(Ana, LoginCodePurpose.Login, Code));
        Assert.NotEqual(hash, hasher.Hash(Beto, LoginCodePurpose.Login, Code));
        Assert.NotEqual(hash, hasher.Hash(Ana, LoginCodePurpose.Signup, Code));
        Assert.NotEqual(hash, hasher.Hash(Ana, LoginCodePurpose.Login, "123457"));
        Assert.DoesNotContain(Code, hash, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAECAwQFBgcICQoLDA0ODw==")]
    public void Missing_invalid_or_short_hash_keys_are_rejected(string key)
    {
        var options = new LoginCodeHashOptions { HashKey = key };
        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), [], true));
    }

    [Fact]
    public void Secure_tokens_are_random_url_safe_and_store_only_their_hash()
    {
        var generator = new SecureTokenGenerator();
        var tokens = Enumerable.Range(0, 100).Select(_ => generator.Generate()).ToArray();

        Assert.All(tokens, token => Assert.True(ISecureTokenGenerator.HasTokenFormat(token)));
        Assert.Equal(tokens.Length, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(64, generator.Hash(tokens[0]).Length);
        Assert.NotEqual(tokens[0], generator.Hash(tokens[0]));
        Assert.Equal(generator.Hash(tokens[0]), generator.Hash(tokens[0]));
    }

    [Fact]
    public void Payload_is_ciphertext_and_only_the_same_key_ring_can_recover_it()
    {
        var protector = new PayloadProtector(new EphemeralDataProtectionProvider());
        var otherProtector = new PayloadProtector(new EphemeralDataProtectionProvider());
        const string payload = "private notification payload";

        var ciphertext = protector.Protect(payload);

        Assert.DoesNotContain(payload, ciphertext, StringComparison.Ordinal);
        Assert.Equal(payload, protector.Unprotect(ciphertext));
        Assert.Throws<CryptographicException>(() => otherProtector.Unprotect(ciphertext));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(ciphertext + "tampered"));
    }
}
