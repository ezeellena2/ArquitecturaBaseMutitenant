using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;

/// <summary>Limita la reautenticación a acciones admitidas y exige un método objetivo solo cuando esa acción lo necesita.</summary>
internal sealed class RequestReauthRequestValidator : AbstractValidator<RequestReauthRequest>
{
    public RequestReauthRequestValidator()
    {
        RuleFor(request => request.Action).Must(action => action is ReauthAction.RemoveMethod or ReauthAction.MakePrimary
            or ReauthAction.DeleteAccount).WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.TargetMethodId).NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .When(request => request.Action is ReauthAction.RemoveMethod or ReauthAction.MakePrimary);
        RuleFor(request => request.TargetMethodId).Null().WithMessage(_ => ValidationTexts.Required)
            .When(request => request.Action == ReauthAction.DeleteAccount);
    }
}
