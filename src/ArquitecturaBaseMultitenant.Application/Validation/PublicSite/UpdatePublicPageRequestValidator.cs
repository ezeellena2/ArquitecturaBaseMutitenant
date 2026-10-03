using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.PublicSite;

internal sealed class UpdatePublicPageRequestValidator : AbstractValidator<UpdatePublicPageRequest>
{
    public UpdatePublicPageRequestValidator()
    {
        RuleFor(request => request.Slug).Must(value => TenantSlug.Create(value).IsSuccess)
            .WithMessage(_ => ValidationTexts.PageInvalid);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(TextLimits.OrganizationName);
        RuleFor(request => request.Description).MaximumLength(TextLimits.Description);
        RuleFor(request => request.ContactPhoneNumber).MaximumLength(TextLimits.PhoneNumber);
        RuleFor(request => request.Address).MaximumLength(TextLimits.Description);
        RuleFor(request => request.Website).MaximumLength(TextLimits.WebAddress).Must(IsWebAddress)
            .When(request => !string.IsNullOrWhiteSpace(request.Website));
        RuleFor(request => request.LogoBase64).MaximumLength((PublicPage.MaximumLogoBytes + 2) / 3 * 4)
            .Must(IsBase64).When(request => request.LogoBase64 is not null);
        RuleFor(request => request.LogoContentType).Must(type => type is "image/png" or "image/jpeg" or "image/webp")
            .When(request => request.LogoBase64 is not null);
        RuleFor(request => request).Must(request => !(request.RemoveLogo && request.LogoBase64 is not null))
            .WithMessage(_ => ValidationTexts.PageInvalid);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }

    private static bool IsBase64(string? value)
    {
        if (value is null || value.Length > (PublicPage.MaximumLogoBytes + 2) / 3 * 4) return false;
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
