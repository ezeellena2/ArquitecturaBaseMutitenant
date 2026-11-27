using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>
/// Las reglas de forma que comparten el alta y el guardado de un catálogo. El largo del texto y del detalle, y el valor según el
/// tipo, los decide el dominio con su código (<c>Forms.Catalog.ItemTextTooLong</c>, <c>ValueInvalid</c>…); acá va lo que el
/// dominio da por cumplido: un código y un texto presentes, y el código dentro de su largo.
/// </summary>
internal static class FormCatalogInputRules
{
    public static string TooMany => FormsTexts.Get("Forms.Validation.TooMany");
    public static string Repeated => FormsTexts.Get("Forms.Validation.Repeated");

    /// <summary>Una opción del pedido; en el alta todas son nuevas, con su id local y sin id del servidor.</summary>
    public static void Item(InlineValidator<FormCatalogItemInput> item, bool created)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.RuleFor(input => input.Code).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(FormsTextLimits.OptionCode).WithMessage(_ => ValidationTexts.MaxLength);
        item.RuleFor(input => input.Text).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        item.RuleFor(input => input.IsActive).NotNull().WithMessage(_ => ValidationTexts.Required);
        item.RuleFor(input => input.LocalId).NotEmpty().When(input => created || input.ItemId is null)
            .WithMessage(_ => ValidationTexts.Required);
        item.RuleFor(input => input.ItemId).Null().When(_ => created)
            .WithMessage(_ => FormsTexts.Get(FormCatalogErrors.ItemNotFoundCode));
        item.RuleFor(input => input.ItemId).NotEqual(Guid.Empty).WithMessage(_ => FormsTexts.Get(FormCatalogErrors.ItemNotFoundCode));
    }

    /// <summary>Los filtros de miembros o de contactos: su forma y sus topes; las columnas y los operadores los valida la fuente.</summary>
    public static void Filters(InlineValidator<FormCatalogFiltersInput> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        filters.RuleFor(input => input.SourceVersion).NotNull().WithMessage(_ => ValidationTexts.Required);
        filters.RuleFor(input => input.ExcludeRecipient).NotNull().WithMessage(_ => ValidationTexts.Required);
        filters.RuleFor(input => input.Conditions).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(conditions => conditions!.Count <= FormCatalogLimits.MaxConditions).WithMessage(_ => TooMany);
        filters.RuleForEach(input => input.Conditions).ChildRules(condition =>
        {
            condition.RuleFor(input => input.Column).NotEmpty().WithMessage(_ => ValidationTexts.Required);
            condition.RuleFor(input => input.Operator).NotEmpty().WithMessage(_ => ValidationTexts.Required);
            condition.RuleFor(input => input.Values).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(_ => ValidationTexts.Required)
                .Must(values => values!.Count <= FormCatalogLimits.MaxConditionValues).WithMessage(_ => TooMany);
            condition.RuleForEach(input => input.Values).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        });
        filters.RuleFor(input => input.Sort).ChildRules(sort =>
        {
            sort.RuleFor(input => input!.Column).NotEmpty().WithMessage(_ => ValidationTexts.Required);
            sort.RuleFor(input => input!.Descending).NotNull().WithMessage(_ => ValidationTexts.Required);
        }).When(input => input.Sort is not null);
    }

    /// <summary>
    /// Cada id local y cada id de opción va una sola vez entre las opciones del pedido; si se repite, se marcan todas las que lo
    /// llevan, en <c>items.{i}</c>. Se cuenta de una pasada: ninguna opción se compara contra todas las demás, así un pedido con
    /// muchas opciones llega rápido al tope del servicio (<c>TooManyItems</c>, 409).
    /// </summary>
    public static void AddRepeated<T>(IReadOnlyList<FormCatalogItemInput?>? items, ValidationContext<T> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (items is null) return;
        var ids = new Dictionary<Guid, int>();
        var locals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item?.ItemId is { } itemId) ids[itemId] = ids.GetValueOrDefault(itemId) + 1;
            else if (item is { LocalId.Length: > 0 }) locals[item.LocalId] = locals.GetValueOrDefault(item.LocalId) + 1;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var repeated = items[index] switch
            {
                { ItemId: { } itemId } => ids[itemId] > 1,
                { LocalId: { Length: > 0 } localId } => locals[localId] > 1,
                _ => false,
            };
            if (repeated) context.AddFailure($"{context.PropertyPath}[{index}]", Repeated);
        }
    }
}
