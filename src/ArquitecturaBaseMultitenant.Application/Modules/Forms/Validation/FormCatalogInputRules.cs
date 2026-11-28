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

    /// <summary>
    /// Un campo de las opciones (E13): uno guardado con su id o uno nuevo con su id local, con nombre, tipo y sus dos reglas; la clave,
    /// si viene, con su forma, y el largo de un texto de 1 a 300. Que el tipo admita las reglas, la fuente y la clave repetida los
    /// decide el dominio (<c>Forms.Catalog.FieldInvalid</c> y <c>FieldKeyDuplicate</c>), y más de 20, también (<c>TooManyFields</c>).
    /// </summary>
    public static void Field(InlineValidator<FormCatalogFieldInput> field)
    {
        ArgumentNullException.ThrowIfNull(field);
        field.RuleFor(input => input.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(FormsTextLimits.FieldName).WithMessage(_ => ValidationTexts.MaxLength);
        field.RuleFor(input => input.Type).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(_ => ValidationTexts.Required)
            .IsInEnum().WithMessage(_ => ValidationTexts.Required);
        field.RuleFor(input => input.Required).NotNull().WithMessage(_ => ValidationTexts.Required);
        field.RuleFor(input => input.Unique).NotNull().WithMessage(_ => ValidationTexts.Required);
        field.RuleFor(input => input.LocalId).NotEmpty().When(input => input.Id is null).WithMessage(_ => ValidationTexts.Required);
        field.RuleFor(input => input.Id).NotEqual(Guid.Empty).WithMessage(_ => FieldInvalid);
        field.RuleFor(input => input.Key).Must(FormCatalogField.IsValidKey).When(input => input.Key is not null).WithMessage(_ => FieldInvalid);
        field.RuleFor(input => input.MaxLength).InclusiveBetween(1, FormsTextLimits.FieldTextLength).WithMessage(_ => FieldInvalid);
        field.RuleFor(input => input.SourceCatalogId).NotEqual(Guid.Empty).WithMessage(_ => FieldInvalid);
    }

    /// <summary>Qué va en WhatsApp (E13): el título y la descripción, cada uno con hasta 24 lugares; que existan lo decide el dominio.</summary>
    public static void Presentation(InlineValidator<FormCatalogPresentationInput> presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        presentation.RuleFor(input => input.Title).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(places => places!.Count <= FormCatalogLimits.MaxPresentationPlaces).WithMessage(_ => TooMany);
        presentation.RuleFor(input => input.Description).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(places => places!.Count <= FormCatalogLimits.MaxPresentationPlaces).WithMessage(_ => TooMany);
        presentation.RuleForEach(input => input.Title).NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .When(input => input.Title is not { Count: > FormCatalogLimits.MaxPresentationPlaces });
        presentation.RuleForEach(input => input.Description).NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .When(input => input.Description is not { Count: > FormCatalogLimits.MaxPresentationPlaces });
    }

    /// <summary>
    /// Si el id o el id local de <paramref name="field"/> va una sola vez entre los campos del pedido; si se repite, se marcan todos,
    /// en <c>fields.{i}</c>. Son hasta 20, así compararlos de a pares no pesa.
    /// </summary>
    public static bool IsOnceIn(IReadOnlyList<FormCatalogFieldInput?>? fields, FormCatalogFieldInput? field)
    {
        if (fields is null || field is null) return true;
        return field switch
        {
            { Id: { } id } => fields.Count(other => other?.Id == id) == 1,
            { LocalId: { Length: > 0 } localId } => fields.Count(other => other is { Id: null }
                && string.Equals(other.LocalId, localId, StringComparison.Ordinal)) == 1,
            _ => true,
        };
    }

    private static string FieldInvalid => FormsTexts.Get(FormCatalogErrors.FieldInvalidCode);

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
