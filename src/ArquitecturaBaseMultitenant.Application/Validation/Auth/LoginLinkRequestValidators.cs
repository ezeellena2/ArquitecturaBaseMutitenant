using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;
namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;
internal sealed class PreviewLoginLinkRequestValidator : AbstractValidator<PreviewLoginLinkRequest>
{
    public PreviewLoginLinkRequestValidator() => RuleFor(request => request.Token)
        .Must(ISecureTokenGenerator.HasTokenFormat).WithMessage(_ => ValidationTexts.LoginLinkTokenFormat);
}
internal sealed class RedeemLoginLinkRequestValidator : AbstractValidator<RedeemLoginLinkRequest>
{
    public RedeemLoginLinkRequestValidator()
    {
        RuleFor(request => request.Token).Must(ISecureTokenGenerator.HasTokenFormat).WithMessage(_ => ValidationTexts.LoginLinkTokenFormat);
        RuleFor(request => request.Access).Equal("consumer").WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}
