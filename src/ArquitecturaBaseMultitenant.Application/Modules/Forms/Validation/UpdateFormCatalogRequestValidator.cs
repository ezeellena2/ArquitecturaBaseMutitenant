using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>
/// Guardar todo junto (C3). <c>isActive</c> y la versión son obligatorios; una opción por borrar va una sola vez y no puede ir
/// también en <c>items</c> ni en <c>moves</c>; «Ordenar por…» no se combina con movimientos ni con borrados (224). Que cada opción
/// sea del catálogo, el tope y el código único los comprueba el servicio bajo el bloqueo.
/// </summary>
internal sealed class UpdateFormCatalogRequestValidator : AbstractValidator<UpdateFormCatalogRequest>
{
    public UpdateFormCatalogRequestValidator()
    {
        RuleFor(request => request.Name).ShortName();
        RuleFor(request => request.Description).MaxLength(FormsTextLimits.CatalogDescription);
        RuleFor(request => request.IsActive).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.Items).NotNull().WithMessage(_ => ValidationTexts.Required)
            .ChildRules(item => FormCatalogInputRules.Item(item, created: false));
        RuleForEach(request => request.Items).Must((request, item) => FormCatalogInputRules.IsUnique(request.Items, item))
            .WithMessage(_ => FormCatalogInputRules.Repeated);
        RuleFor(request => request.Moves).Must(moves => moves is null || moves.Count <= FormCatalogLimits.MaxMoves)
            .WithMessage(_ => FormCatalogInputRules.TooMany);
        RuleForEach(request => request.Moves).NotNull().WithMessage(_ => ValidationTexts.Required).ChildRules(move =>
        {
            move.RuleFor(input => input.ItemId).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(_ => ValidationTexts.Required)
                .NotEqual(Guid.Empty).WithMessage(_ => FormsTexts.Get(FormCatalogErrors.ItemNotFoundCode));
            move.RuleFor(input => input.Direction).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(_ => ValidationTexts.Required)
                .IsInEnum().WithMessage(_ => ValidationTexts.Required);
        });
        RuleFor(request => request.DeleteItems).Must(ids => ids is null || ids.Count <= FormCatalogLimits.MaxDeleteItems)
            .WithMessage(_ => FormCatalogInputRules.TooMany);
        RuleForEach(request => request.DeleteItems)
            .NotEqual(Guid.Empty).WithMessage(_ => FormsTexts.Get(FormCatalogErrors.ItemNotFoundCode))
            .Must(IsDeletedOnce).WithMessage(_ => FormCatalogInputRules.Repeated);
        RuleFor(request => request.Sort).ChildRules(sort =>
        {
            sort.RuleFor(input => input!.Column).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(_ => ValidationTexts.Required)
                .IsInEnum().WithMessage(_ => ValidationTexts.Required);
            sort.RuleFor(input => input!.Descending).NotNull().WithMessage(_ => ValidationTexts.Required);
        }).When(request => request.Sort is not null);
        RuleFor(request => request.Sort)
            .Must((request, sort) => sort is null || (request.Moves is not { Count: > 0 } && request.DeleteItems is not { Count: > 0 }))
            .WithMessage(_ => FormsTexts.Get("Forms.Validation.SortCombined"));
        RuleFor(request => request.Filters).ChildRules(filters => FormCatalogInputRules.Filters(filters!)).When(request => request.Filters is not null);
    }

    /// <summary>Una opción por borrar va una sola vez y no va también entre las opciones ni entre los movimientos.</summary>
    private static bool IsDeletedOnce(UpdateFormCatalogRequest request, Guid id) =>
        request.DeleteItems!.Count(other => other == id) == 1
        && !(request.Items ?? []).Any(item => item?.ItemId == id)
        && !(request.Moves ?? []).Any(move => move?.ItemId == id);
}
