using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

internal sealed class RequestLoginCodeRequestValidator : AbstractValidator<RequestLoginCodeRequest>
{
    public RequestLoginCodeRequestValidator() =>
        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
}
