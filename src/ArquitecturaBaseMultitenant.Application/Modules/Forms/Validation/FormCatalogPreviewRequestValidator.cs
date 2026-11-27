using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>
/// La forma de la vista previa: el origen y sus filtros. Que el origen tenga filtros y que las columnas, los operadores y la persona
/// o el cliente de ejemplo correspondan a la fuente lo decide el servicio.
/// </summary>
internal sealed class FormCatalogPreviewRequestValidator : AbstractValidator<FormCatalogPreviewRequest>
{
    public FormCatalogPreviewRequestValidator()
    {
        RuleFor(request => request.Source).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(_ => ValidationTexts.Required)
            .IsInEnum().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Filters).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Filters).ChildRules(filters => FormCatalogInputRules.Filters(filters!)).When(request => request.Filters is not null);
    }
}
