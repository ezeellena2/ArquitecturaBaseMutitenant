using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.PublicSite;

internal sealed class UpdatePublicPageRequestValidator : AbstractValidator<UpdatePublicPageRequest>
{
    private const int MaximumLogoBase64Length = (PublicPage.MaximumLogoBytes + 2) / 3 * 4;

    public UpdatePublicPageRequestValidator()
    {
        RuleFor(request => request.Slug).Must(value => TenantSlug.Create(value).IsSuccess)
            .WithMessage(_ => ValidationTexts.SlugInvalid);
        RuleFor(request => request.Name).OrganizationName();
        RuleFor(request => request.Description).Description();
        RuleFor(request => request.ContactPhoneNumber).MaxLength(TextLimits.PhoneNumber);
        RuleFor(request => request.Address).MaxLength(TextLimits.Description);
        RuleFor(request => request.Website).Cascade(CascadeMode.Stop)
            .MaxLength(TextLimits.WebAddress)
            .Must(IsWebAddress).WithMessage(_ => ValidationTexts.WebsiteInvalid)
            .When(request => !string.IsNullOrWhiteSpace(request.Website));
        RuleFor(request => request.LogoBase64).Null().WithMessage(_ => ValidationTexts.LogoRemoveConflict)
            .When(request => request.RemoveLogo);
        RuleFor(request => request.LogoBase64).Must(IsBase64).WithMessage(_ => ValidationTexts.LogoInvalid)
            .When(request => !request.RemoveLogo && request.LogoBase64 is not null);
        RuleFor(request => request.LogoContentType)
            .Must(type => type is "image/png" or "image/jpeg" or "image/webp")
            .WithMessage(_ => ValidationTexts.LogoInvalid)
            .When(request => !request.RemoveLogo && request.LogoBase64 is not null);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }

    private static bool IsBase64(string? value)
    {
        if (value is null || value.Length > MaximumLogoBase64Length) return false;
        var buffer = new byte[(value.Length / 4 + 1) * 3];
        return Convert.TryFromBase64String(value, buffer, out _);
    }

    private static bool IsWebAddress(string? value) => value is null ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

internal sealed class SetPublicPagePublicationRequestValidator : AbstractValidator<SetPublicPagePublicationRequest>
{
    public SetPublicPagePublicationRequestValidator() =>
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
}
