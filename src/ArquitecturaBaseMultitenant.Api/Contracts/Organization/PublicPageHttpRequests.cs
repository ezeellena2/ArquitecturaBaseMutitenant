using ArquitecturaBaseMultitenant.Application.Models.PublicSite;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record UpdatePublicPageHttpRequest(string? Slug, string? Name, string? Description,
    string? ContactEmail, string? ContactPhone, string? Address, string? Website,
    string? LogoBase64, string? LogoContentType, bool RemoveLogo, uint? Version)
{
    public override string ToString() => nameof(UpdatePublicPageHttpRequest);
}

public sealed record SetPublicPagePublicationHttpRequest(uint? Version)
{
    public override string ToString() => nameof(SetPublicPagePublicationHttpRequest);
}

public static class PublicPageHttpMappings
{
    public static UpdatePublicPageRequest ToModel(this UpdatePublicPageHttpRequest request) =>
        new(request.Slug, request.Name, request.Description, request.ContactEmail, request.ContactPhone,
            request.Address, request.Website, request.LogoBase64, request.LogoContentType,
            request.RemoveLogo, request.Version);

    public static SetPublicPagePublicationRequest ToModel(this SetPublicPagePublicationHttpRequest request) =>
        new(request.Version);
}
