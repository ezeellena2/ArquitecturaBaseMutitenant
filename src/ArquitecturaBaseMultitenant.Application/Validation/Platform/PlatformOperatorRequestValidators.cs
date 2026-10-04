using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

/// <summary>Exige el correo; los permisos los valida el dominio (al menos uno, del catálogo y sin repetir).</summary>
internal sealed class InvitePlatformOperatorRequestValidator : AbstractValidator<InvitePlatformOperatorRequest>
{
    public InvitePlatformOperatorRequestValidator() =>
        RuleFor(r => r.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
}

/// <summary>Exige versión y motivo; los permisos los valida el dominio.</summary>
internal sealed class ChangePlatformOperatorRequestValidator : AbstractValidator<ChangePlatformOperatorRequest>
{
    public ChangePlatformOperatorRequestValidator()
    {
        RuleFor(r => r.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(r => r.Reason).Required().Description();
    }
}
