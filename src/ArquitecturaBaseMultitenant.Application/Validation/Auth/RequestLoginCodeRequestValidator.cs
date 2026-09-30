using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

/// <summary>Exige un correo antes de pedir un código de ingreso; el tipo Email representa su valor normalizado.</summary>
internal sealed class RequestLoginCodeRequestValidator : AbstractValidator<RequestLoginCodeRequest>
{
    public RequestLoginCodeRequestValidator() =>
        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
}
