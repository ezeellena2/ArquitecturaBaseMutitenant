using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

/// <summary>Exige correo y aceptación de términos antes de enviar el código de registro.</summary>
internal sealed class SignupRequestValidator : AbstractValidator<SignupRequest>
{
    public SignupRequestValidator()
    {
        RuleFor(request => request.Email).NotNull().When(request => request.Phone is null).WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.AcceptedTerms).Equal(true).WithMessage(_ => ValidationTexts.Required);
    }
}
