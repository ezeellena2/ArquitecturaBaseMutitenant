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
/// sea del catálogo, el tope y el código único los comprueba el servicio bajo el bloqueo. Desde la E13, los campos, la presentación y
/// los campos que se quitan con sus valores llevan su forma y sus topes; el resto, también el campo de <c>sort.column</c>, lo decide
/// el servicio bajo el bloqueo.
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
        RuleForEach(request => request.Items).Must((request, item, context) => !FormCatalogInputRules.IsRepeated(request.Items, item, context))
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
        }).When(request => request.Moves is not { Count: > FormCatalogLimits.MaxMoves });
        RuleFor(request => request.DeleteItems).Must(ids => ids is null || ids.Count <= FormCatalogLimits.MaxDeleteItems)
            .WithMessage(_ => FormCatalogInputRules.TooMany);
        RuleForEach(request => request.DeleteItems)
            .NotEqual(Guid.Empty).WithMessage(_ => FormsTexts.Get(FormCatalogErrors.ItemNotFoundCode))
            .Must((request, id, context) => !IsRepeatedDeletion(request, id, context)).WithMessage(_ => FormCatalogInputRules.Repeated)
            .When(request => request.DeleteItems is not { Count: > FormCatalogLimits.MaxDeleteItems });
        RuleFor(request => request.Sort).ChildRules(sort =>
        {
            // Text, Code o la identidad de un campo; que el campo sea uno activo guardado lo comprueba el servicio bajo el bloqueo.
            sort.RuleFor(input => input!.Column).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(_ => ValidationTexts.Required)
                .Must((input, _) => input!.FixedColumn is not null || input.FieldId is not null).WithMessage(_ => ValidationTexts.Required);
            sort.RuleFor(input => input!.Descending).NotNull().WithMessage(_ => ValidationTexts.Required);
        }).When(request => request.Sort is not null);
        RuleFor(request => request.Sort)
            .Must((request, sort) => sort is null || (request.Moves is not { Count: > 0 } && request.DeleteItems is not { Count: > 0 }))
            .WithMessage(_ => FormsTexts.Get("Forms.Validation.SortCombined"));
        RuleForEach(request => request.Fields).NotNull().WithMessage(_ => ValidationTexts.Required).ChildRules(FormCatalogInputRules.Field)
            .When(request => request.Fields is not { Count: > FormCatalogLimits.MaxFields });
        RuleForEach(request => request.Fields).Must((request, field) => FormCatalogInputRules.IsOnceIn(request.Fields, field))
            .WithMessage(_ => FormCatalogInputRules.Repeated).When(request => request.Fields is not { Count: > FormCatalogLimits.MaxFields });
        RuleFor(request => request.Presentation).ChildRules(presentation => FormCatalogInputRules.Presentation(presentation!))
            .When(request => request.Presentation is not null);
        RuleFor(request => request.ConfirmedFieldRemovals)
            .Must(ids => ids is null || ids.Count <= FormCatalogLimits.MaxFields).WithMessage(_ => FormCatalogInputRules.TooMany);
        RuleForEach(request => request.ConfirmedFieldRemovals)
            .NotEqual(Guid.Empty).WithMessage(_ => FormsTexts.Get(FormCatalogErrors.FieldInvalidCode))
            .When(request => request.ConfirmedFieldRemovals is not { Count: > FormCatalogLimits.MaxFields });
        RuleFor(request => request.Filters).ChildRules(filters => FormCatalogInputRules.Filters(filters!)).When(request => request.Filters is not null);
    }

    private const string DeletionCountsKey = "Forms.Catalog.DeletionCounts";

    /// <summary>
    /// Una opción por borrar va una sola vez y no va también entre las opciones ni entre los movimientos; la regla la marca en
    /// <c>deleteItems.{i}</c>. Las cuentas se arman de una pasada, una vez por validación, y quedan en el contexto: no se recorren
    /// las opciones del pedido por cada borrado.
    /// </summary>
    private static bool IsRepeatedDeletion(UpdateFormCatalogRequest request, Guid id, ValidationContext<UpdateFormCatalogRequest> context)
    {
        var counts = context.RootContextData.TryGetValue(DeletionCountsKey, out var cached) && cached is DeletionCounts known
            && ReferenceEquals(known.Request, request)
            ? known
            : DeletionCounts.Of(request);
        context.RootContextData[DeletionCountsKey] = counts;
        return counts.Ids.GetValueOrDefault(id) > 1 || counts.Elsewhere.Contains(id);
    }

    /// <summary>Cuántas veces va cada borrado, y los ids de opción que el pedido también manda en las opciones o en los movimientos.</summary>
    private sealed record DeletionCounts(UpdateFormCatalogRequest Request, Dictionary<Guid, int> Ids, HashSet<Guid> Elsewhere)
    {
        public static DeletionCounts Of(UpdateFormCatalogRequest request)
        {
            var ids = new Dictionary<Guid, int>();
            foreach (var id in request.DeleteItems ?? []) ids[id] = ids.GetValueOrDefault(id) + 1;
            var elsewhere = (request.Items ?? []).Select(item => item?.ItemId).Concat((request.Moves ?? []).Select(move => move?.ItemId))
                .OfType<Guid>().ToHashSet();
            return new DeletionCounts(request, ids, elsewhere);
        }
    }
}
