using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

/// <summary>Valida consentimiento y retornos antes de iniciar el desafío OAuth.</summary>
internal sealed class ExternalLoginStartRequestValidator : AbstractValidator<ExternalLoginStartRequest>
{
    public ExternalLoginStartRequestValidator()
    {
        RuleFor(request => request.Signup).Must((request, signup) => signup == request.SignupEndpoint)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.AcceptedTerms).Equal(true).When(request => request.Signup)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ReturnTo).Must(ReturnUrls.IsLocalAppPath).When(request => request.Signup)
            .WithMessage(_ => ValidationTexts.ReturnUrlInvalid);
        RuleFor(request => request.ReturnUrl).Must(ReturnUrls.IsLoginReturn).When(request => !request.Signup)
            .WithMessage(_ => ValidationTexts.ReturnUrlInvalid);
    }
}
