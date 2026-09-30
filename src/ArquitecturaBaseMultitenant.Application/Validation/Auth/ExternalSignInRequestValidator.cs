using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

/// <summary>Exige un retorno de autorización seguro y, solo para altas con Google, aceptación de términos.</summary>
internal sealed class ExternalSignInRequestValidator : AbstractValidator<ExternalSignInRequest>
{
    public ExternalSignInRequestValidator()
    {
        RuleFor(request => request.ReturnUrl)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(ReturnUrls.IsAuthorizeRequest).WithMessage(_ => ValidationTexts.ReturnUrlInvalid);
        RuleFor(request => request.AcceptedTerms)
            .Equal(true).When(request => request.IsSignup).WithMessage(_ => ValidationTexts.Required);
    }
}
