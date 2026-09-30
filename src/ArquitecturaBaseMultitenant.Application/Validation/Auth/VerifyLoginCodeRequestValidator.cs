using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

/// <summary>Comprueba correo, longitud y dígitos del código y un retorno de acceso permitido antes de verificar el ingreso.</summary>
internal sealed class VerifyLoginCodeRequestValidator : AbstractValidator<VerifyLoginCodeRequest>
{
    public VerifyLoginCodeRequestValidator(IOptions<LoginCodeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var length = options.Value.Length;

        RuleFor(request => request.Email).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(code => code!.Length == length && code.All(char.IsAsciiDigit))
            .WithMessage(_ => ValidationTexts.LoginCodeFormat);
        RuleFor(request => request.ReturnUrl)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(value => ReturnUrls.TryReadAccessSelection(value, out _))
            .WithMessage(_ => ValidationTexts.ReturnUrlInvalid);
    }
}
