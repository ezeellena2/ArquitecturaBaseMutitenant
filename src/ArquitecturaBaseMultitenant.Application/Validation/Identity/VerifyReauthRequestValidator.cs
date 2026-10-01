using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Identity;

/// <summary>Comprueba acción, métodos de origen y destino y código antes de emitir el ticket para una operación sensible.</summary>
internal sealed class VerifyReauthRequestValidator : AbstractValidator<VerifyReauthRequest>
{
    public VerifyReauthRequestValidator()
    {
        RuleFor(request => request.Action).Must(action => action is ReauthAction.RemoveMethod or ReauthAction.MakePrimary
            or ReauthAction.DeleteAccount or ReauthAction.AddEmail or ReauthAction.LinkGoogle).WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.TargetMethodId).NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .When(request => request.Action is ReauthAction.RemoveMethod or ReauthAction.MakePrimary);
        RuleFor(request => request.TargetMethodId).Null().WithMessage(_ => ValidationTexts.Required)
            .When(request => request.Action is ReauthAction.DeleteAccount or ReauthAction.AddEmail or ReauthAction.LinkGoogle);
        RuleFor(request => request.SourceMethodId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Code).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(code => code!.Length == 6 && code.All(char.IsAsciiDigit)).WithMessage(_ => ValidationTexts.LoginCodeFormat);
    }

}
