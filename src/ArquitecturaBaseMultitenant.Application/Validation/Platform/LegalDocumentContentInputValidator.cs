using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

internal sealed class LegalDocumentContentInputValidator : AbstractValidator<LegalDocumentContentInput>
{
    public LegalDocumentContentInputValidator()
    {
        RuleFor(item => item.Culture).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(item => item.Text).Cascade(CascadeMode.Stop)
            .Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.LegalDocument).WithMessage(_ => ValidationTexts.MaxLength);
    }
}
