using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using PublicPageModelResponse = ArquitecturaBaseMultitenant.Application.Models.PublicSite.PublicPageResponse;

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

public sealed record PublicPageResponse(string? Slug, string Name, string? Description,
    string? LogoDataUri, string? ContactEmail, string? ContactPhone, string? Address,
    string? Website, bool IsPublished, DateTime? PublishBlockedAtUtc,
    string? PublishBlockedReason, uint Version);

public static class PublicPageHttpMappings
{
    public static Result<UpdatePublicPageRequest> ToModel(this UpdatePublicPageHttpRequest request)
    {
        Email? contactEmail = null;
        if (!string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            var parsed = Email.Create(request.ContactEmail);
            if (parsed.IsFailure)
                return new ValidationError(new Dictionary<string, string[]>
                {
                    ["contactEmail"] = [ValidationTexts.EmailInvalid],
                });
            contactEmail = parsed.Value;
        }

        return new UpdatePublicPageRequest(request.Slug, request.Name, request.Description, contactEmail, request.ContactPhone,
            request.Address, request.Website, request.LogoBase64, request.LogoContentType,
            request.RemoveLogo, request.Version);
    }

    public static SetPublicPagePublicationRequest ToModel(this SetPublicPagePublicationHttpRequest request) =>
        new(request.Version);

    public static PublicPageResponse ToHttpResponse(this PublicPageModelResponse response) => new(response.Slug,
        response.Name, response.Description, response.LogoDataUri, response.ContactEmail?.DisplayValue,
        response.ContactPhone, response.Address, response.Website, response.IsPublished,
        response.PublishBlockedAtUtc, response.PublishBlockedReason, response.Version);
}
