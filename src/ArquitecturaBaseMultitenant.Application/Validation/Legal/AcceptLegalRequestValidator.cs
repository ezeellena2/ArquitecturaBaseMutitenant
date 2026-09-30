using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Legal;

/// <summary>Exige identificadores y versiones legales válidos, sin duplicados, antes de registrar la aceptación.</summary>
internal sealed class AcceptLegalRequestValidator : AbstractValidator<AcceptLegalRequest>
{
    public AcceptLegalRequestValidator()
    {
        RuleFor(request => request.Documents).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(_ => ValidationTexts.Required)
            .Must(documents => documents!.Count <= 2 && documents.All(item => item.Id != Guid.Empty && item.Version > 0)
                && documents.Select(item => item.Id).Distinct().Count() == documents.Count)
            .WithMessage(_ => ValidationTexts.Required);
    }
}
