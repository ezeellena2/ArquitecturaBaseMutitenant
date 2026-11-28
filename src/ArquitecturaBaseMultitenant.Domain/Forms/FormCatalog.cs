using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Un catálogo de opciones de la organización (E12, §3 y §5). El origen y el tipo del valor no cambian después del alta; uno de
/// miembros o de contactos es siempre de texto y lleva filtros, y uno a mano lleva opciones (<see cref="FormCatalogItem"/>), cuya
/// versión es la del catálogo (C3). Los textos los valida el validador del pedido antes de llegar acá. Desde la E13, uno a mano lleva
/// los campos de sus opciones (<see cref="FieldsJson"/>, también los quitados) y todos, qué se ve en WhatsApp
/// (<see cref="Presentation"/>), los dos como jsonb.
/// </summary>
public sealed class FormCatalog : Entity, ITenantOwned, IAuditable, IVersioned
{
    private FormCatalog() { }
    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public FormCatalogSource Source { get; private set; }
    public FormCatalogValueType ValueType { get; private set; }
    /// <summary>Los filtros como jsonb (<see cref="FormCatalogFilters"/>): solo en miembros y contactos.</summary>
    public string? Filters { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>Los campos de las opciones como jsonb (<see cref="FormCatalogField"/>), activos y quitados; vacío si no es a mano.</summary>
    public string FieldsJson { get; private set; } = EmptyFields;
    /// <summary>Qué arma el título y la descripción en WhatsApp, como jsonb (<see cref="FormCatalogPresentation"/>).</summary>
    public string Presentation { get; private set; } = FormCatalogPresentation.Default.ToJson();

    private const string EmptyFields = "[]";
    private string? _readFieldsJson;
    private IReadOnlyList<FormCatalogField> _readFields = [];

    public static FormCatalog CreateManual(string name, string? description, FormCatalogValueType valueType)
    {
        if (!Enum.IsDefined(valueType)) throw new ArgumentOutOfRangeException(nameof(valueType));
        var catalog = new FormCatalog { Source = FormCatalogSource.Manual, ValueType = valueType };
        catalog.Rename(name, description);
        return catalog;
    }

    public static FormCatalog CreateMembers(string name, string? description, FormCatalogFilters filters) =>
        CreateFiltered(FormCatalogSource.Members, name, description, filters);

    public static FormCatalog CreateContacts(string name, string? description, FormCatalogFilters filters) =>
        CreateFiltered(FormCatalogSource.Contacts, name, description, filters);

    public void Rename(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        if (trimmedName.Length > TextLimits.ShortName) throw new ArgumentException("The name is too long.", nameof(name));
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > FormsTextLimits.CatalogDescription)
            throw new ArgumentException("The description is too long.", nameof(description));
        Name = trimmedName;
        Description = trimmedDescription;
    }

    /// <summary>
    /// Cambia los filtros de un catálogo de miembros o de contactos; uno a mano no tiene filtros. Los mismos filtros no cambian la
    /// columna: la base devuelve el <c>jsonb</c> con otro orden y otros espacios, así que se compara lo que dicen, no el texto, y un
    /// guardado sin cambios no audita un cambio de filtros.
    /// </summary>
    public Result ChangeFilters(FormCatalogFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (Source == FormCatalogSource.Manual) return FormCatalogErrors.WrongSource;
        var next = filters.ToJson();
        if (!string.Equals(ReadFilters()?.ToJson(), next, StringComparison.Ordinal)) Filters = next;
        return Result.Success();
    }

    public FormCatalogFilters? ReadFilters() => Filters is null ? null : FormCatalogFilters.FromJson(Filters);

    /// <summary>Si se pueden cambiar las opciones: solo en uno a mano y activo.</summary>
    public Result EnsureItemsCanChange()
    {
        if (Source != FormCatalogSource.Manual) return FormCatalogErrors.WrongSource;
        return IsActive ? Result.Success() : FormCatalogErrors.Inactive;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Por qué un catálogo activo «No se puede usar», calculado al leer y sin guardarse (§5); <c>null</c> si se puede usar o si está
    /// dado de baja, que es su propio estado. Uno a mano mira solo sus opciones activas. Uno de contactos sin el canal no tiene con
    /// qué comparar la fuente, así que ese motivo va primero; en los demás manda el orden de <see cref="FormCatalogUnusableReason"/>.
    /// </summary>
    /// <param name="activeItems">Las opciones activas de uno a mano.</param>
    /// <param name="currentSourceVersion">La versión de hoy de la fuente de miembros o de contactos.</param>
    /// <param name="channelAvailable">Si la organización tiene el canal de los contactos (WhatsApp).</param>
    /// <param name="roleMissing">Si falta un rol de una condición.</param>
    /// <param name="companyMissing">Si falta una empresa de una condición.</param>
    public FormCatalogUnusableReason? UnusableReason(int activeItems, int currentSourceVersion, bool channelAvailable,
        bool roleMissing, bool companyMissing) =>
        UnusableReason(new FormCatalogState(Source, IsActive, Source == FormCatalogSource.Manual ? null : ReadFilters()), activeItems,
            currentSourceVersion, channelAvailable, roleMissing, companyMissing);

    /// <summary>
    /// La misma regla sobre lo que se leyó de la base sin la entidad, como la fila del listado o el detalle: un lector proyecta el
    /// catálogo y sus cuentas, y el servicio calcula el estado con esta regla, la única.
    /// </summary>
    public static FormCatalogUnusableReason? UnusableReason(FormCatalogState catalog, int activeItems, int currentSourceVersion,
        bool channelAvailable, bool roleMissing, bool companyMissing)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!catalog.IsActive) return null;
        if (catalog.Source == FormCatalogSource.Manual) return activeItems > 0 ? null : FormCatalogUnusableReason.NoActiveOptions;
        if (catalog.Source == FormCatalogSource.Contacts && !channelAvailable) return FormCatalogUnusableReason.ChannelUnavailable;
        var filters = catalog.Filters ?? throw new InvalidOperationException("A filtered catalog needs its filters.");
        if (filters.SourceVersion != currentSourceVersion) return FormCatalogUnusableReason.SourceOutdated;
        if (catalog.Source == FormCatalogSource.Contacts) return null;
        if (roleMissing) return FormCatalogUnusableReason.RoleMissing;
        return companyMissing ? FormCatalogUnusableReason.CompanyMissing : null;
    }

    /// <summary>
    /// Los campos guardados en su orden, activos y quitados. Se leen una vez por cada versión de la columna, así guardar miles de
    /// opciones no relee la lista en cada una.
    /// </summary>
    public IReadOnlyList<FormCatalogField> ReadFields()
    {
        if (!ReferenceEquals(_readFieldsJson, FieldsJson))
        {
            _readFields = [.. FormCatalogField.ListFromJson(FieldsJson).OrderBy(field => field.Position)];
            _readFieldsJson = FieldsJson;
        }

        return _readFields;
    }

    public FormCatalogPresentation ReadPresentation() => FormCatalogPresentation.FromJson(Presentation);

    /// <summary>
    /// Deja los campos activos de uno a mano en el orden de <paramref name="drafts"/> (la lista entera, E13): uno guardado que no está
    /// queda quitado, con su clave y sus valores, y sale de la presentación; uno quitado no vuelve. Hasta 20 activos, y hasta 100
    /// guardados contando los quitados. La clave de uno guardado no cambia; la de uno nuevo nace del nombre si no viene y, si es un
    /// lugar de la presentación o la de un quitado, suma un número. Un campo nuevo nace opcional. Si nada cambió, la columna no
    /// cambia. Devuelve los activos en su orden, con la identidad de los nuevos; que la fuente de uno «De un catálogo» exista en la
    /// organización y que los valores admitan el tipo o la regla nueva lo comprueba el servicio.
    /// </summary>
    /// <param name="drafts">Los campos activos, en su orden.</param>
    /// <param name="keyTakenBy">El texto de <see cref="FormCatalogErrors.FieldKeyDuplicate"/> con el nombre del otro campo, armado
    /// por el servicio con la cultura del pedido.</param>
    public Result<IReadOnlyList<FormCatalogField>> ChangeFields(IReadOnlyList<FormCatalogFieldDraft> drafts, Func<string, string> keyTakenBy)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(keyTakenBy);
        if (Source != FormCatalogSource.Manual)
            return drafts.Count == 0 ? Result.Success<IReadOnlyList<FormCatalogField>>([]) : FormCatalogErrors.FieldInvalid(0);
        if (drafts.Count > FormCatalogField.MaxActive) return FormCatalogErrors.TooManyFields;

        var stored = ReadFields();
        var byId = stored.ToDictionary(field => field.Id);
        var keptIds = drafts.Where(draft => draft.Id is not null).Select(draft => draft.Id!.Value).ToHashSet();
        var removed = stored.Where(field => !field.IsActive || !keptIds.Contains(field.Id)).ToList();
        if (removed.Count + drafts.Count > FormCatalogField.MaxStored) return FormCatalogErrors.TooManyFields;

        // Las claves de los guardados que siguen activos van primero: son fijas, y una nueva que choca con una de ellas es el error.
        var removedKeys = removed.Select(field => field.Key).ToHashSet(StringComparer.Ordinal);
        var activeKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var draft in drafts)
            if (draft.Id is { } id && byId.TryGetValue(id, out var saved) && saved.IsActive) activeKeys.TryAdd(saved.Key, saved.Name);

        var seen = new HashSet<Guid>();
        var active = new List<FormCatalogField>(drafts.Count);
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            FormCatalogField? previous = null;
            if (draft.Id is { } id && (!byId.TryGetValue(id, out previous) || !previous.IsActive || !seen.Add(id)))
                return FormCatalogErrors.FieldInvalid(index);

            string key;
            if (previous is not null)
            {
                if (draft.Key is not null && !string.Equals(draft.Key, previous.Key, StringComparison.Ordinal)) return FormCatalogErrors.FieldInvalid(index);
                key = previous.Key;
            }
            else
            {
                if (draft.Required || (draft.Key is not null && !FormCatalogField.IsValidKey(draft.Key))) return FormCatalogErrors.FieldInvalid(index);
                key = FormCatalogField.FreeKey(draft.Key ?? FormCatalogField.KeyFromName(draft.Name), removedKeys);
                if (activeKeys.TryGetValue(key, out var otherName)) return FormCatalogErrors.FieldKeyDuplicate(index, keyTakenBy(otherName));
                activeKeys.Add(key, draft.Name.Trim());
            }

            var field = BuildField(draft, previous?.Id ?? Guid.CreateVersion7(), key, active.Count);
            if (field is null) return FormCatalogErrors.FieldInvalid(index);
            active.Add(field);
        }

        var next = FormCatalogField.ToJson([.. active, .. removed.Select((field, offset) => field with { Position = active.Count + offset, IsActive = false })]);
        if (!string.Equals(FormCatalogField.ToJson(stored), next, StringComparison.Ordinal)) FieldsJson = next;
        var current = ReadPresentation();
        var presentation = current.WithoutFields(removed.Select(field => field.Id).ToHashSet()).ToJson();
        if (!string.Equals(current.ToJson(), presentation, StringComparison.Ordinal)) Presentation = presentation;
        return active;
    }

    /// <summary>
    /// Cambia qué arma el título y la descripción en WhatsApp de uno a mano (E13): cada lugar existe (el valor, solo si es tipado, y
    /// un campo, solo si está activo), no se repite en su lista y el título tiene al menos uno. El lugar de un campo queda con su
    /// identidad en minúsculas. Si nada cambió, la columna no cambia.
    /// </summary>
    public Result ChangePresentation(FormCatalogPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(presentation.Title);
        ArgumentNullException.ThrowIfNull(presentation.Description);
        if (Source != FormCatalogSource.Manual) return FormCatalogErrors.PresentationInvalid;

        var activeFields = ReadFields().Where(field => field.IsActive).Select(field => field.Id).ToHashSet();
        var title = Places(presentation.Title, activeFields);
        var description = Places(presentation.Description, activeFields);
        if (title is null || title.Count == 0 || description is null) return FormCatalogErrors.PresentationInvalid;

        var next = new FormCatalogPresentation(title, description).ToJson();
        if (!string.Equals(ReadPresentation().ToJson(), next, StringComparison.Ordinal)) Presentation = next;
        return Result.Success();
    }

    /// <summary>Los lugares que existen, sin repetir y con el de cada campo normalizado; <c>null</c> si alguno no.</summary>
    private List<string>? Places(IReadOnlyList<string> places, HashSet<Guid> activeFields)
    {
        var result = new List<string>(places.Count);
        foreach (var place in places)
        {
            var normalized = place switch
            {
                FormCatalogPresentation.Text or FormCatalogPresentation.Code or FormCatalogPresentation.Detail => place,
                FormCatalogPresentation.Value => ValueType == FormCatalogValueType.Text ? null : place,
                _ => FormCatalogPresentation.FieldIdOf(place) is { } id && activeFields.Contains(id) ? FormCatalogPresentation.Field(id) : null,
            };
            if (normalized is null || result.Contains(normalized, StringComparer.Ordinal)) return null;
            result.Add(normalized);
        }

        return result;
    }

    /// <summary>El campo con las reglas que admite su tipo, o <c>null</c> si alguna no corresponde (E13).</summary>
    private FormCatalogField? BuildField(FormCatalogFieldDraft draft, Guid id, string key, int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Name);
        var name = draft.Name.Trim();
        if (name.Length > FormsTextLimits.FieldName) throw new ArgumentException("The field name is too long.", nameof(draft));
        if (!Enum.IsDefined(draft.Type)) return null;

        var isCatalog = draft.Type == FormCatalogFieldType.Catalog;
        if (isCatalog != draft.SourceCatalogId is not null) return null;
        if (isCatalog && (draft.SourceCatalogId == Guid.Empty || draft.SourceCatalogId == Id)) return null;
        if (draft.MaxLength is { } maxLength && (draft.Type != FormCatalogFieldType.Text || maxLength is < 1 or > FormsTextLimits.FieldTextLength))
            return null;

        var min = string.IsNullOrWhiteSpace(draft.Min) ? null : draft.Min.Trim();
        var max = string.IsNullOrWhiteSpace(draft.Max) ? null : draft.Max.Trim();
        var limits = draft.Type switch
        {
            FormCatalogFieldType.WholeNumber or FormCatalogFieldType.DecimalNumber =>
                NumberLimits(min, max, draft.Type == FormCatalogFieldType.WholeNumber),
            FormCatalogFieldType.Date => DateLimits(min, max),
            _ => min is null && max is null ? (null, null) : ((string? Min, string? Max)?)null,
        };
        if (limits is not { } canonical) return null;

        return new FormCatalogField(id, key, name, draft.Type, draft.SourceCatalogId, draft.Required, draft.Unique, draft.MaxLength,
            canonical.Min, canonical.Max, position, IsActive: true);
    }

    /// <summary>El mínimo y el máximo canónicos de un número, o <c>null</c> si alguno no es del tipo o el mínimo supera al máximo.</summary>
    private static (string? Min, string? Max)? NumberLimits(string? min, string? max, bool wholeNumber)
    {
        decimal? low = null, high = null;
        if (min is not null)
        {
            if (!FormCatalogValueText.TryParseNumber(min, wholeNumber, out var value)) return null;
            low = value;
        }

        if (max is not null)
        {
            if (!FormCatalogValueText.TryParseNumber(max, wholeNumber, out var value)) return null;
            high = value;
        }

        if (low > high) return null;
        return (low is { } from ? FormCatalogValueText.Number(from) : null, high is { } to ? FormCatalogValueText.Number(to) : null);
    }

    /// <summary>El mínimo y el máximo de una fecha, o <c>null</c> si alguno no es <c>yyyy-MM-dd</c> o el mínimo es posterior.</summary>
    private static (string? Min, string? Max)? DateLimits(string? min, string? max)
    {
        DateOnly? low = null, high = null;
        if (min is not null)
        {
            if (!FormCatalogValueText.TryParseDate(min, out var value)) return null;
            low = value;
        }

        if (max is not null)
        {
            if (!FormCatalogValueText.TryParseDate(max, out var value)) return null;
            high = value;
        }

        if (low > high) return null;
        return (low is { } from ? FormCatalogValueText.Date(from) : null, high is { } to ? FormCatalogValueText.Date(to) : null);
    }

    private static FormCatalog CreateFiltered(FormCatalogSource source, string name, string? description, FormCatalogFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var catalog = new FormCatalog { Source = source, ValueType = FormCatalogValueType.Text, Filters = filters.ToJson() };
        catalog.Rename(name, description);
        return catalog;
    }
}
