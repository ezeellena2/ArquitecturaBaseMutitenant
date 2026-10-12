using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Auth;

/// <summary>URIs del cliente público "web" (el SPA), en Authentication:Clients:Web.</summary>
public sealed class WebClientOptions
{
    public const string SectionName = "Authentication:Clients:Web";

    [MinLength(1)]
    public IList<Uri> RedirectUris { get; init; } = [];

    [MinLength(1)]
    public IList<Uri> PostLogoutRedirectUris { get; init; } = [];
}
