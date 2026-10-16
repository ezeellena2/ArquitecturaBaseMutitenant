using ArquitecturaBaseMultitenant.Api.Hosting;

namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Hosting;

/// <summary>
/// Lo que el alta embebida de números necesita en la CSP del SPA (E9): el SDK de Meta desde connect.facebook.net, sus
/// marcos de facebook.com y las conexiones del SDK a esos orígenes. Sale con el módulo.
/// </summary>
internal sealed class MetaContentSecurityPolicySource : IContentSecurityPolicySource
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Sources { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["script-src"] = ["https://connect.facebook.net"],
            ["frame-src"] = ["https://facebook.com", "https://*.facebook.com"],
            ["connect-src"] = ["https://connect.facebook.net", "https://facebook.com", "https://*.facebook.com"],
        };
}
