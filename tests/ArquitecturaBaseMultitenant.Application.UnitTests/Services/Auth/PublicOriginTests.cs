using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Auth;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>Conserva el origen configurado, los escapes y el fallback de los avisos.</summary>
public sealed class PublicOriginTests
{
    [Theory]
    [InlineData("https://example.test", "/cuenta", "https://example.test/cuenta")]
    [InlineData("https://example.test/base/", "/login", "https://example.test/login")]
    [InlineData("https://example.test:8443/base/", "/registro?returnTo=%2Forg%3Fq%3Da%26b%3Dc", "https://example.test:8443/registro?returnTo=%2Forg%3Fq%3Da%26b%3Dc")]
    [InlineData("https://example.test/base/", "/invitacion#opaque-token", "https://example.test/invitacion#opaque-token")]
    [InlineData(null, "/cuenta", "/cuenta")]
    [InlineData(null, "/login?error=Auth.ExternalLogin.Failed", "/login?error=Auth.ExternalLogin.Failed")]
    public void Links_keep_authority_queries_fragments_and_relative_fallback(string? issuer, string path, string expected)
    {
        IPublicOrigin origin = new Origin(issuer is null ? null : new Uri(issuer));
        Assert.Equal(expected, origin.Absolute(path));
    }

    [Fact]
    public void Account_and_registration_destinations_do_not_become_authorized_login_returns()
    {
        foreach (var path in new[] { ReturnUrls.AccountPath, ReturnUrls.SignupPath, ReturnUrls.BusinessSignupPath,
            ReturnUrls.PlatformPath, ReturnUrls.BusinessLoginPath, ReturnUrls.LoginPath })
        {
            Assert.False(ReturnUrls.IsLoginReturn(path));
            Assert.False(ReturnUrls.IsDeletionReturn(path));
            Assert.False(ReturnUrls.TryReadLoginCodeSelection(path, null, out _));
        }
    }

    private sealed class Origin(Uri? value) : IPublicOrigin
    {
        public Uri? Value { get; } = value;
    }
}
