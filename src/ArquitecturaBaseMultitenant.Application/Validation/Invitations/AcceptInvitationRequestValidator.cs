using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Invitations;

internal sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(request => request.Token).MaximumLength(4096).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.AcceptedTerms).Equal(true).WithMessage(_ => ValidationTexts.Required)
            .When(request => request.RequiresRegistration);
    }
}
