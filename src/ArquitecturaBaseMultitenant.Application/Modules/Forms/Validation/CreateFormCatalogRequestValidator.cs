using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>
/// El alta: nombre, origen y, en uno de miembros o de contactos, sus filtros. Que las opciones o los filtros correspondan al origen
/// lo decide el servicio (<c>Forms.Catalog.WrongSource</c>).
/// </summary>
internal sealed class CreateFormCatalogRequestValidator : AbstractValidator<CreateFormCatalogRequest>
{
    public CreateFormCatalogRequestValidator()
    {
        RuleFor(request => request.Name).ShortName();
        RuleFor(request => request.Description).MaxLength(FormsTextLimits.CatalogDescription);
        RuleFor(request => request.Source).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(_ => ValidationTexts.Required)
            .IsInEnum().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ValueType).IsInEnum().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.Items).NotNull().WithMessage(_ => ValidationTexts.Required)
            .ChildRules(item => FormCatalogInputRules.Item(item, created: true));
        RuleForEach(request => request.Items).Must((request, item) => FormCatalogInputRules.IsUnique(request.Items, item))
            .WithMessage(_ => FormCatalogInputRules.Repeated);
        RuleFor(request => request.Filters).NotNull().When(request => request.Source is FormCatalogSource.Members or FormCatalogSource.Contacts)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Filters).ChildRules(filters => FormCatalogInputRules.Filters(filters!)).When(request => request.Filters is not null);
    }
}
