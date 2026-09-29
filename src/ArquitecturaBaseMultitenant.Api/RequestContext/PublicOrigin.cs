using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Api.RequestContext;

/// <summary>El origen público es el issuer configurado, independiente del Host de la petición.</summary>
internal sealed class PublicOrigin(IConfiguration configuration) : IPublicOrigin
{
    public Uri? Value { get; } = Parse(configuration["Authentication:Issuer"]);

    private static Uri? Parse(string? issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return null;
        }

        var origin = new Uri(issuer, UriKind.Absolute);
        return origin.AbsolutePath.EndsWith('/') ? origin : new Uri(origin.AbsoluteUri + "/");
    }
}
