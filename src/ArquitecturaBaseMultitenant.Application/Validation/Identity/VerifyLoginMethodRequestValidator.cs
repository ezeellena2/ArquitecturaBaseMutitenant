using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;

internal sealed class VerifyLoginMethodRequestValidator : AbstractValidator<VerifyLoginMethodRequest>
{
    public VerifyLoginMethodRequestValidator()
    {
        RuleFor(request => request.MethodId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Code).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(code => code!.Length == 6 && code.All(char.IsAsciiDigit))
            .WithMessage(_ => ValidationTexts.LoginCodeFormat);
    }
}
