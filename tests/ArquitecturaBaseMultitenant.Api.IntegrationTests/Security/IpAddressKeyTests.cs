using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Security;

/// <summary>
/// N20 y ADR 0063: una IPv6 cuenta por su /64 en las cuotas de pedidos, en el tope de códigos por WhatsApp y en los topes
/// de correo; una IPv4 escrita como IPv6 es la misma IPv4. Ningún hash muestra la dirección.
/// </summary>
public sealed class IpAddressKeyTests
{
    private static readonly IOptions<LoginCodeHashOptions> Key =
        Options.Create(new LoginCodeHashOptions { HashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) });

    [Fact]
    public void Two_ipv6_of_the_same_64_share_a_key_and_another_64_does_not()
    {
        Assert.Equal("2001:db8:85a3:8d3::/64", IpAddressKey.Normalize("2001:db8:85a3:8d3:1319:8a2e:370:7348"));
        Assert.Equal(IpAddressKey.Normalize("2001:db8:85a3:8d3::1"), IpAddressKey.Normalize("2001:db8:85a3:8d3:ffff:ffff:ffff:ffff"));
        Assert.NotEqual(IpAddressKey.Normalize("2001:db8:85a3:8d3::1"), IpAddressKey.Normalize("2001:db8:85a3:8d4::1"));
        Assert.Equal("192.0.2.10", IpAddressKey.Normalize("::ffff:192.0.2.10"));
        Assert.Equal(IpAddressKey.Unknown, IpAddressKey.Normalize(null));
        Assert.Equal(IpAddressKey.Unknown, IpAddressKey.Normalize("no es una dirección"));
    }

    [Fact]
    public void Each_hasher_counts_an_ipv6_by_its_64()
    {
        const string first = "2001:db8:1:2:aaaa::1";
        const string second = "2001:db8:1:2:bbbb::2";
        const string other = "2001:db8:1:3::1";
        var rateLimit = new RateLimitIpHasher(Key);
        var whatsApp = new WhatsAppQuotaIpHasher(Key);
        var email = new MessageQuotaKeyHasher(Key);
        var day = new DateOnly(2026, 11, 24);

        Assert.Equal(rateLimit.Hash(first), rateLimit.Hash(second));
        Assert.NotEqual(rateLimit.Hash(first), rateLimit.Hash(other));
        Assert.Equal(whatsApp.Hash(first, "number", day), whatsApp.Hash(second, "number", day));
        Assert.NotEqual(whatsApp.Hash(first, "number", day), whatsApp.Hash(other, "number", day));
        Assert.Equal(email.ForAddress(first), email.ForAddress(second));
        Assert.NotEqual(email.ForAddress(first), email.ForAddress(other));
        Assert.Equal(email.ForAddress("192.0.2.1"), email.ForAddress("::ffff:192.0.2.1"));
        Assert.DoesNotContain("2001", email.ForAddress(first), StringComparison.Ordinal);
    }

    [Fact]
    public void A_subject_gives_another_key_in_each_quota()
    {
        var email = new MessageQuotaKeyHasher(Key);
        var id = Guid.NewGuid();

        Assert.NotEqual(email.ForSubject(MessageQuotaScope.CodeAccount, id), email.ForSubject(MessageQuotaScope.Inviter, id));
        Assert.NotEqual(email.ForSubject(MessageQuotaScope.CodeAccount, id), email.ForSubject(MessageQuotaScope.WhatsAppCodeAccount, id));
        Assert.DoesNotContain(id.ToString(), email.ForSubject(MessageQuotaScope.Organization, id), StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentOutOfRangeException>(() => email.ForSubject(MessageQuotaScope.Global, id));
    }
}
