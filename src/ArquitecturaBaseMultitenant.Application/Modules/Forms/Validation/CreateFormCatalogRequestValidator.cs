using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>
/// El alta: nombre, origen y, en uno de miembros o de contactos, sus filtros. Que las opciones o los filtros correspondan al origen
/// lo decide el servicio (<c>Forms.Catalog.WrongSource</c>). Los campos y la presentación (E13), con su forma; uno que no es a mano
/// no los lleva (<c>Forms.Catalog.FieldInvalid</c> y <c>PresentationInvalid</c>, del dominio).
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
        RuleFor(request => request.Items).Custom(FormCatalogInputRules.AddRepeated);
        RuleForEach(request => request.Fields).NotNull().WithMessage(_ => ValidationTexts.Required).ChildRules(FormCatalogInputRules.Field)
            .When(request => request.Fields is not { Count: > FormCatalogLimits.MaxFields });
        RuleForEach(request => request.Fields).Must((request, field) => FormCatalogInputRules.IsOnceIn(request.Fields, field))
            .WithMessage(_ => FormCatalogInputRules.Repeated).When(request => request.Fields is not { Count: > FormCatalogLimits.MaxFields });
        RuleFor(request => request.Presentation).ChildRules(presentation => FormCatalogInputRules.Presentation(presentation!))
            .When(request => request.Presentation is not null);
        RuleFor(request => request.Filters).NotNull().When(request => request.Source is FormCatalogSource.Members or FormCatalogSource.Contacts)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Filters).ChildRules(filters => FormCatalogInputRules.Filters(filters!)).When(request => request.Filters is not null);
    }
}
