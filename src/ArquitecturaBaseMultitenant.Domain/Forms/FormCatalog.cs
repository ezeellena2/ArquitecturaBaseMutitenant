using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Un catálogo de opciones de la organización (E12, §3 y §5). El origen y el tipo del valor no cambian después del alta; uno de
/// miembros o de contactos es siempre de texto y lleva filtros, y uno a mano lleva opciones (<see cref="FormCatalogItem"/>), cuya
/// versión es la del catálogo (C3). Los textos los valida el validador del pedido antes de llegar acá.
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

    /// <summary>Cambia los filtros de un catálogo de miembros o de contactos; uno a mano no tiene filtros.</summary>
    public Result ChangeFilters(FormCatalogFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (Source == FormCatalogSource.Manual) return FormCatalogErrors.WrongSource;
        Filters = filters.ToJson();
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
        bool roleMissing, bool companyMissing)
    {
        if (!IsActive) return null;
        if (Source == FormCatalogSource.Manual) return activeItems > 0 ? null : FormCatalogUnusableReason.NoActiveOptions;
        if (Source == FormCatalogSource.Contacts && !channelAvailable) return FormCatalogUnusableReason.ChannelUnavailable;
        var filters = ReadFilters() ?? throw new InvalidOperationException("A filtered catalog needs its filters.");
        if (filters.SourceVersion != currentSourceVersion) return FormCatalogUnusableReason.SourceOutdated;
        if (Source == FormCatalogSource.Contacts) return null;
        if (roleMissing) return FormCatalogUnusableReason.RoleMissing;
        return companyMissing ? FormCatalogUnusableReason.CompanyMissing : null;
    }

    private static FormCatalog CreateFiltered(FormCatalogSource source, string name, string? description, FormCatalogFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var catalog = new FormCatalog { Source = source, ValueType = FormCatalogValueType.Text, Filters = filters.ToJson() };
        catalog.Rename(name, description);
        return catalog;
    }
}
