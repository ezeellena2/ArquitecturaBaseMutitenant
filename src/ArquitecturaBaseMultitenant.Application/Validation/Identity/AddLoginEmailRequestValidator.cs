using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;

/// <summary>Exige un correo antes de iniciar el alta de otro método de ingreso; el tipo Email ya representa el valor validado.</summary>
internal sealed class AddLoginEmailRequestValidator : AbstractValidator<AddLoginEmailRequest>
{
    public AddLoginEmailRequestValidator() =>
        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
}
