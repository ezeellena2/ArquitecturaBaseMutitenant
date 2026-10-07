using ArquitecturaBaseMultitenant.Application.Models.PublicSite;

namespace ArquitecturaBaseMultitenant.Api.Contracts.PublicSite;

/// <summary>Solo los datos que la organización publicó para quienes visitan su subdominio.</summary>
public sealed record PublicSiteHttpResponse(string Slug, string Name, string? Description,
    string? LogoUrl, string? Address, string? Phone, string? Email, string? Website)
{
    public static PublicSiteHttpResponse FromModel(PublicSiteResponse response) => new(response.Slug,
        response.Name, response.Description, response.LogoUrl, response.Address, response.Phone?.Value,
        response.Email?.DisplayValue, response.Website);
}
