using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;

internal sealed class AddLoginEmailRequestValidator : AbstractValidator<AddLoginEmailRequest>
{
    public AddLoginEmailRequestValidator() =>
        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
}
