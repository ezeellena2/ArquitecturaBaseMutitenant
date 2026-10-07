using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

internal sealed class PreviewAccountRecoveryRequestValidator : AbstractValidator<PreviewAccountRecoveryRequest>
{
    public PreviewAccountRecoveryRequestValidator() =>
        RuleFor(request => request.PreviousEmail).NotNull().WithMessage(_ => ValidationTexts.Required);
}

/// <summary>Exige el correo nuevo y un nonce con forma de token antes de emitir el código de recuperación.</summary>
internal sealed class RequestRecoveryCodeRequestValidator : AbstractValidator<RequestRecoveryCodeRequest>
{
    public RequestRecoveryCodeRequestValidator()
    {
        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Nonce).RecoveryNonce();
    }
}

/// <summary>Exige correo, nonce y un código de la longitud configurada antes de verificarlo.</summary>
internal sealed class VerifyRecoveryCodeRequestValidator : AbstractValidator<VerifyRecoveryCodeRequest>
{
    public VerifyRecoveryCodeRequestValidator(IOptions<LoginCodeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var length = options.Value.Length;

        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Nonce).RecoveryNonce();
        RuleFor(request => request.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(code => code!.Length == length && code.All(char.IsAsciiDigit))
            .WithMessage(_ => ValidationTexts.LoginCodeFormat);
    }
}

/// <summary>Exige un solo método de antes, distinto del nuevo; el teléfono lo interpreta el servicio.</summary>
internal sealed class SubmitAccountRecoveryRequestValidator : AbstractValidator<SubmitAccountRecoveryRequest>
{
    public SubmitAccountRecoveryRequestValidator()
    {
        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Nonce).RecoveryNonce();
        RuleFor(request => request.PreviousEmail)
            .Cascade(CascadeMode.Stop)
            .Must((request, previous) => previous is not null || HasPhone(request))
            .WithMessage(_ => ValidationTexts.Required)
            .Must((request, previous) => previous is null || !HasPhone(request))
            .WithMessage(_ => ValidationTexts.EmailOrPhone)
            .Must((request, previous) => previous is null || previous != request.Email)
            .WithMessage(_ => ValidationTexts.RecoveryMethodRepeated);
    }

    private static bool HasPhone(SubmitAccountRecoveryRequest request) =>
        !string.IsNullOrWhiteSpace(request.PreviousPhoneNumber);
}

internal static class RecoveryNonceRules
{
    public static IRuleBuilderOptions<T, string?> RecoveryNonce<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(ISecureTokenGenerator.HasTokenFormat).WithMessage(_ => ValidationTexts.NonceFormat);
}
