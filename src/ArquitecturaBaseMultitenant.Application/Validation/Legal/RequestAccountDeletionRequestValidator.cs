using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Legal;

internal sealed class RequestAccountDeletionRequestValidator : AbstractValidator<RequestAccountDeletionRequest>
{
    public RequestAccountDeletionRequestValidator() => RuleFor(request => request.Reason)
        .NotEmpty().WithMessage(_ => ValidationTexts.Required).MaxLength(TextLimits.Description);
}
