using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;
namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;
internal sealed class AddLoginMethodRequestValidator : AbstractValidator<AddLoginMethodRequest>
{
    public AddLoginMethodRequestValidator() => RuleFor(request => request.Email).NotNull()
        .When(request => request.Phone is null).WithMessage(_ => ValidationTexts.Required);
}
