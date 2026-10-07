using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;

/// <summary>Exige el método objetivo antes de cambiarlo o quitarlo.</summary>
internal sealed class ChangeLoginMethodRequestValidator : AbstractValidator<ChangeLoginMethodRequest>
{
    public ChangeLoginMethodRequestValidator()
    {
        RuleFor(request => request.MethodId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
    }
}
