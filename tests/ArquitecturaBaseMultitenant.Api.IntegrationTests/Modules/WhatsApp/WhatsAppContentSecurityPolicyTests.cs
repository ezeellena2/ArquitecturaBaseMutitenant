using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// El alta embebida de números (E9): con el módulo instalado, la CSP del SPA permite el SDK de Meta, sus marcos de
/// facebook.com y las conexiones del SDK, y nada más. Sin scripts en línea ni eval. Sale con el módulo.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppContentSecurityPolicyTests(ApiFactory factory)
{
    [Fact]
    public async Task The_spa_policy_allows_the_meta_sdk_and_its_frames_without_inline_scripts()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/alive", TestContext.Current.CancellationToken);

        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Equal(["'self'", "https://connect.facebook.net"], Directive(policy, "script-src"));
        Assert.Equal(["'self'", "https://facebook.com", "https://*.facebook.com"], Directive(policy, "frame-src"));
        Assert.Equal(["'self'", "https://connect.facebook.net", "https://facebook.com", "https://*.facebook.com"],
            Directive(policy, "connect-src"));
    }

    private static string[] Directive(string policy, string name) =>
        policy.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Single(part => part[0] == name)[1..];
}
