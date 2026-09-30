using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;

internal sealed class ChangeLoginMethodRequestValidator : AbstractValidator<ChangeLoginMethodRequest>
{
    public ChangeLoginMethodRequestValidator()
    {
        RuleFor(request => request.MethodId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ReauthTicket).Must(ISecureTokenGenerator.HasTokenFormat)
            .WithMessage(_ => ValidationTexts.Required);
    }
}
