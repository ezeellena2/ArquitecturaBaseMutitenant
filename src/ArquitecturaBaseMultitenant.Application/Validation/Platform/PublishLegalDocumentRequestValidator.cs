using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

/// <summary>Tipo, fecha y textos presentes; que estén todas las culturas habilitadas lo decide el servicio.</summary>
internal sealed class PublishLegalDocumentRequestValidator : AbstractValidator<PublishLegalDocumentRequest>
{
    public PublishLegalDocumentRequestValidator()
    {
        RuleFor(request => request.Kind).NotNull().WithMessage(_ => ValidationTexts.Required)
            .IsInEnum().WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.EffectiveOn).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Contents).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.Contents).SetValidator(new LegalDocumentContentInputValidator());
    }
}
