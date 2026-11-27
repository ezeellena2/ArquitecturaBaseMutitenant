using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using CatalogCondition = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogCondition;
using CatalogFilters = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogFilters;
using CatalogFilterSort = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogFilterSort;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Decide sobre los filtros de un catálogo de miembros o de contactos contra su fuente (C2, C5 y C12 de la E12), sin escribir:
/// <list type="bullet">
/// <item>al crear, guardar o ver la vista previa, que cada condición use una columna y un operador de la fuente, con valores que
/// existen en la organización, y la persona o el cliente de ejemplo;</item>
/// <item>al leer, el estado «No se puede usar» de cada catálogo con su motivo, y los roles y las empresas con nombre que usan
/// sus filtros: un id que ya no existe no viene, y el catálogo queda <c>RoleMissing</c> o <c>CompanyMissing</c>.</item>
/// </list>
/// Forms nunca referencia a WhatsApp: los contactos llegan por <see cref="IStructuredContactSource"/>.
/// </summary>
internal sealed class FormCatalogFiltersPolicy(IMemberDirectoryReader members, IStructuredContactSource contacts)
{
    /// <summary>La persona de ejemplo en uno de miembros y el cliente de ejemplo en uno de contactos; se guarda en los filtros.</summary>
    public static Guid? SampleOf(FormCatalogSource source, Guid? sampleMemberId, Guid? sampleContactId) => source switch
    {
        FormCatalogSource.Members => sampleMemberId,
        FormCatalogSource.Contacts => sampleContactId,
        _ => null,
    };

    /// <summary>La definición de hoy de la fuente, con su versión; uno a mano no tiene fuente.</summary>
    public static FormCatalogSourceResponse? DefinitionOf(FormCatalogSource source) => source switch
    {
        FormCatalogSource.Members => MembersCatalogSource.Definition,
        FormCatalogSource.Contacts => ContactsCatalogSource.Definition,
        _ => null,
    };

    /// <summary>
    /// Los filtros del dominio, con su persona o cliente de ejemplo, si corresponden a la fuente; si no, el error de su campo. Uno
    /// de miembros con condiciones pide la persona de ejemplo (<see cref="FormCatalogErrors.SampleRequired"/>).
    /// </summary>
    public async Task<Result<CatalogFilters>> DecideAsync(FormCatalogSource source, FormCatalogFiltersInput input, Guid? sampleId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (DefinitionOf(source) is not { } definition) return FormCatalogErrors.WrongSource;
        if (input.SourceVersion != definition.Version) return FormCatalogErrors.FiltersFieldInvalid("sourceVersion");
        var conditions = input.Conditions ?? [];
        for (var index = 0; index < conditions.Count; index++)
        {
            if (!HasValidShape(definition, conditions[index])) return FormCatalogErrors.FiltersInvalid(index);
        }

        if (input.Detail is { Length: > 0 } detail && !definition.Columns.Any(column => column.Key == detail && column.CanDetail))
            return FormCatalogErrors.FiltersFieldInvalid("detail");
        if (input.Sort is { } sort && !definition.Columns.Any(column => column.Key == sort.Column && column.CanSort))
            return FormCatalogErrors.FiltersFieldInvalid("sort");
        if (source == FormCatalogSource.Members && await MissingReferenceAsync(conditions, ct) is { } missing)
            return FormCatalogErrors.FiltersInvalid(missing);

        var sample = await CheckSampleAsync(source, conditions.Count, sampleId, ct);
        if (sample.IsFailure) return sample.Error;
        return new CatalogFilters(definition.Version,
            [.. conditions.Select(condition => new CatalogCondition(condition.Column!, condition.Operator!,
                [.. condition.Values!]))],
            input.ExcludeRecipient!.Value, string.IsNullOrWhiteSpace(input.Detail) ? null : input.Detail,
            input.Sort is null ? null : new CatalogFilterSort(input.Sort.Column!, input.Sort.Descending!.Value), sampleId);
    }

    /// <summary>La persona o el cliente de ejemplo guardado, con su nombre; <c>null</c> si ya no está o no hay canal.</summary>
    public async Task<FormCatalogSample?> FindSampleAsync(FormCatalogSource source, Guid sampleId, CancellationToken ct)
    {
        if (source == FormCatalogSource.Members)
        {
            var member = await members.FindSampleAsync(sampleId, ct);
            return member is null ? null
                : new FormCatalogSample(member.MemberId, member.Name, member.Companies.Count == 0 ? null : string.Join(", ", member.Companies));
        }

        if (source != FormCatalogSource.Contacts) return null;
        var contact = await contacts.FindAsync(sampleId, ct);
        return contact is null ? null : new FormCatalogSample(contact.Id, contact.Name, null);
    }

    /// <summary>
    /// El estado de cada catálogo de una página o de un detalle: los roles y las empresas que faltan se leen de una vez para todos,
    /// y el canal de los contactos, una sola vez. El motivo lo decide la regla del dominio, la única.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, FormCatalogFilterState>> DecideStatesAsync(IReadOnlyList<FormCatalogStoredRow> catalogs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        var memberCatalogs = catalogs.Where(catalog => catalog.Source == FormCatalogSource.Members && catalog.Filters is not null).ToArray();
        var roles = await NamesAsync(memberCatalogs, MembersCatalogSource.Role, members.ListRolesByIdAsync, ct);
        var companies = await NamesAsync(memberCatalogs, MembersCatalogSource.Company, members.ListCompaniesByIdAsync, ct);
        var channel = !catalogs.Any(catalog => catalog.Source == FormCatalogSource.Contacts && catalog.IsActive)
            || await contacts.IsAvailableAsync(ct);

        var states = new Dictionary<Guid, FormCatalogFilterState>();
        foreach (var catalog in catalogs)
        {
            var usedRoles = Values(catalog, MembersCatalogSource.Role);
            var usedCompanies = Values(catalog, MembersCatalogSource.Company);
            var reason = FormCatalog.UnusableReason(new FormCatalogState(catalog.Source, catalog.IsActive, catalog.Filters),
                catalog.ActiveItems, DefinitionOf(catalog.Source)?.Version ?? 0, channel,
                roleMissing: usedRoles.Any(value => !Has(roles, value)), companyMissing: usedCompanies.Any(value => !Has(companies, value)));
            states[catalog.Id] = catalog.Source == FormCatalogSource.Members
                ? new FormCatalogFilterState(reason, Found(roles, usedRoles), Found(companies, usedCompanies))
                : new FormCatalogFilterState(reason, null, null);
        }

        return states;
    }

    private static bool HasValidShape(FormCatalogSourceResponse definition, FormCatalogConditionInput condition)
    {
        var column = definition.Columns.FirstOrDefault(candidate => candidate.Key == condition.Column);
        if (column is null || !column.Operators.Contains(condition.Operator!)) return false;
        // Los valores llegan limpios del contrato (textos-libres, nivel 1); uno vacío o en blanco llega como "".
        var values = condition.Values!.ToArray();
        if (values.Length == 0 || values.Any(value => value.Length == 0 || value.Length > FormCatalogLimits.MaxConditionValueLength))
            return false;
        return column.Type switch
        {
            FormCatalogColumnType.Text => values.Length == 1,
            FormCatalogColumnType.Boolean => values.Length == 1 && values[0] is "true" or "false",
            // Las listas de miembros llevan ids; las etiquetas de un contacto, textos.
            _ => column.Key == ContactsCatalogSource.Tags || values.All(value => Guid.TryParse(value, CultureInfo.InvariantCulture, out var id) && id != Guid.Empty),
        };
    }

    /// <summary>La primera condición de roles o de empresas con un id que no existe en la organización.</summary>
    private async Task<int?> MissingReferenceAsync(IReadOnlyList<FormCatalogConditionInput> conditions, CancellationToken ct)
    {
        var roles = await ExistingAsync(conditions, MembersCatalogSource.Role, members.ListRolesByIdAsync, ct);
        var companies = await ExistingAsync(conditions, MembersCatalogSource.Company, members.ListCompaniesByIdAsync, ct);
        for (var index = 0; index < conditions.Count; index++)
        {
            var existing = conditions[index].Column switch
            {
                MembersCatalogSource.Role => roles,
                MembersCatalogSource.Company => companies,
                _ => null,
            };
            if (existing is not null && conditions[index].Values!.Any(value => !existing.Contains(Guid.Parse(value, CultureInfo.InvariantCulture))))
                return index;
        }

        return null;
    }

    private async Task<Result> CheckSampleAsync(FormCatalogSource source, int conditions, Guid? sampleId, CancellationToken ct)
    {
        if (sampleId is not { } id)
            return source == FormCatalogSource.Members && conditions > 0 ? FormCatalogErrors.SampleRequired : Result.Success();
        if (await FindSampleAsync(source, id, ct) is not null) return Result.Success();
        return source == FormCatalogSource.Members ? FormCatalogErrors.SampleMemberNotFound : FormCatalogErrors.SampleContactNotFound;
    }

    private static async Task<HashSet<Guid>> ExistingAsync(IReadOnlyList<FormCatalogConditionInput> conditions, string column,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyList<MemberDirectoryNameRow>>> read, CancellationToken ct)
    {
        Guid[] ids = [.. conditions.Where(condition => condition.Column == column).SelectMany(condition => condition.Values!)
            .Select(value => Guid.Parse(value, CultureInfo.InvariantCulture)).Distinct()];
        if (ids.Length == 0) return [];
        return [.. (await read(ids, ct)).Select(row => row.Id)];
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<FormCatalogStoredRow> catalogs, string column,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyList<MemberDirectoryNameRow>>> read, CancellationToken ct)
    {
        Guid[] ids = [.. catalogs.SelectMany(catalog => Values(catalog, column))
            .Select(value => Guid.TryParse(value, CultureInfo.InvariantCulture, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct()];
        if (ids.Length == 0) return new Dictionary<Guid, string>();
        return (await read(ids, ct)).ToDictionary(row => row.Id, row => row.Name);
    }

    private static string[] Values(FormCatalogStoredRow catalog, string column) => catalog.Source != FormCatalogSource.Members
        ? []
        : [.. (catalog.Filters?.Conditions ?? []).Where(condition => condition.Column == column).SelectMany(condition => condition.Values)
            .Distinct(StringComparer.Ordinal)];

    private static bool Has(IReadOnlyDictionary<Guid, string> names, string value) =>
        Guid.TryParse(value, CultureInfo.InvariantCulture, out var id) && names.ContainsKey(id);

    private static IReadOnlyList<FormCatalogReference> Found(IReadOnlyDictionary<Guid, string> names, string[] values) =>
        [.. values.Where(value => Has(names, value)).Select(value => Guid.Parse(value, CultureInfo.InvariantCulture)).Distinct()
            .Select(id => new FormCatalogReference(id, names[id])).OrderBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)];
}

/// <summary>El estado calculado de un catálogo: su motivo de «No se puede usar» y los roles y las empresas con nombre de sus filtros.</summary>
internal sealed record FormCatalogFilterState(FormCatalogUnusableReason? Reason, IReadOnlyList<FormCatalogReference>? Roles,
    IReadOnlyList<FormCatalogReference>? Companies);
