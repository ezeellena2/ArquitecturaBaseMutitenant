using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

/// <summary>
/// Método y formato del código del segundo factor (E9), antes de abrir la transacción: un formato inválido responde
/// Validation.Failed y no consume un intento. Sin método válido el error va en method y el código no se juzga.
/// </summary>
internal sealed class VerifyPlatformFactorRequestValidator : AbstractValidator<VerifyPlatformFactorRequest>
{
    public VerifyPlatformFactorRequestValidator()
    {
        RuleFor(request => request.Method).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Code).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Code).Must(TwoFactorCodeFormat.IsAuthenticatorCode)
            .WithMessage(_ => ValidationTexts.TwoFactorCodeFormat)
            .When(request => request.Method == TwoFactorMethod.Authenticator && !string.IsNullOrEmpty(request.Code));
        RuleFor(request => request.Code).Must(code => TwoFactorCodeFormat.NormalizeRecoveryCode(code) is not null)
            .WithMessage(_ => ValidationTexts.RecoveryCodeFormat)
            .When(request => request.Method == TwoFactorMethod.RecoveryCode && !string.IsNullOrEmpty(request.Code));
    }
}

/// <summary>«Guardé los códigos»: sólo vale true.</summary>
internal sealed class CompletePlatformFactorRequestValidator : AbstractValidator<CompletePlatformFactorRequest>
{
    public CompletePlatformFactorRequestValidator()
    {
        RuleFor(request => request.SavedRecoveryCodes)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(_ => ValidationTexts.Required)
            .Equal(true).WithMessage(_ => ValidationTexts.RecoveryCodesSaved);
    }
}

/// <summary>«Confirmá que sos vos»: las mismas reglas de método y formato que el ingreso.</summary>
internal sealed class PlatformReauthenticationRequestValidator : AbstractValidator<PlatformReauthenticationRequest>
{
    public PlatformReauthenticationRequestValidator()
    {
        RuleFor(request => request.Method).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Code).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Code).Must(TwoFactorCodeFormat.IsAuthenticatorCode)
            .WithMessage(_ => ValidationTexts.TwoFactorCodeFormat)
            .When(request => request.Method == TwoFactorMethod.Authenticator && !string.IsNullOrEmpty(request.Code));
        RuleFor(request => request.Code).Must(code => TwoFactorCodeFormat.NormalizeRecoveryCode(code) is not null)
            .WithMessage(_ => ValidationTexts.RecoveryCodeFormat)
            .When(request => request.Method == TwoFactorMethod.RecoveryCode && !string.IsNullOrEmpty(request.Code));
    }
}
