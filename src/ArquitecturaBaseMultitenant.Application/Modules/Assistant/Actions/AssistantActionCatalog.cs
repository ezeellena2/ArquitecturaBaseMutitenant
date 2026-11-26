using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;

/// <summary>Una acción del registro con la fuente de su área, que lista, resuelve y valida sin guardar.</summary>
internal sealed record AssistantActionEntry(AssistantActionDefinition Action, IAssistantActionSource Source);

/// <summary>
/// El registro de las acciones (ADR 0068 §6). Junta las fuentes de las áreas por DI y comprueba que cada id con versión sea
/// único. Filtra en cada llamada por acceso, módulo prendido, requisito, calificación y modelo del turno, con una sola lectura
/// de los permisos por llamada. No nombra a ningún área: el núcleo tampoco lo nombra a él.
/// </summary>
internal sealed class AssistantActionCatalog
{
    private readonly IReadOnlyList<AssistantActionEntry> _entries;
    private readonly AssistantActionQualifications _qualifications;
    private readonly ICurrentUser _user;
    private readonly IFeatureService _features;
    private readonly IPermissionService _permissions;
    private readonly IPlatformPermissionService _platform;

    public AssistantActionCatalog(IEnumerable<IAssistantActionSource> sources, AssistantActionQualifications qualifications,
        ICurrentUser user, IFeatureService features, IPermissionService permissions, IPlatformPermissionService platform)
    {
        _entries = sources.SelectMany(source => source.Definitions.Select(action => new AssistantActionEntry(action, source))).ToArray();
        if (_entries.GroupBy(entry => entry.Action.Key, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } twice)
            throw new InvalidOperationException($"The action {twice.Key} is declared twice.");
        (_qualifications, _user, _features, _permissions, _platform) = (qualifications, user, features, permissions, platform);
    }

    /// <summary>
    /// Lo que se le ofrece a quien pide con el modelo del turno. Sin modelo todavía (al preparar el turno), lo calificado con
    /// algún modelo: el turno sella después solo lo calificado con el suyo (<see cref="Seal"/>).
    /// </summary>
    internal async Task<IReadOnlyList<AssistantActionDefinition>> ListAsync(string? providerModelId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId || _user.Access is not { } access) return [];
        var candidates = _entries.Select(entry => entry.Action)
            .Where(action => IsOfAccess(action.Requirement.Kind, access)
                && (providerModelId is null ? _qualifications.IsOffered(action) : _qualifications.IsQualified(action, providerModelId)))
            .ToArray();
        if (candidates.Length == 0) return [];
        var granted = access == Access.Business ? await _permissions.GetBusinessPermissionsAsync(userId, ct) : null;
        var offered = new List<AssistantActionDefinition>();
        foreach (var action in candidates)
            if ((action.Module is null || await _features.IsEnabledAsync(action.Module, ct))
                && await HoldsAsync(userId, action.Requirement, granted, null, ct)) offered.Add(action);
        return offered;
    }

    /// <summary>Lo que sella el turno: lo preparado que pasó la calificación con su modelo (0068 §6).</summary>
    internal IReadOnlyList<string> Seal(IEnumerable<AssistantActionDefinition> prepared, string providerModelId) =>
        prepared.Where(action => _qualifications.IsQualified(action, providerModelId)).Select(action => action.Key).ToArray();

    /// <summary>Lo sellado que se sigue ofreciendo ahora: cada llamada vuelve a autorizar.</summary>
    internal async Task<IReadOnlyList<AssistantActionDefinition>> ListSealedAsync(IReadOnlyCollection<string> sealedKeys,
        string providerModelId, CancellationToken ct) =>
        sealedKeys.Count == 0 ? [] : (await ListAsync(providerModelId, ct)).Where(action => sealedKeys.Contains(action.Key, StringComparer.Ordinal)).ToArray();

    /// <summary>Una acción del sello que se sigue ofreciendo, con su fuente; null si está fuera del sello o ya no se ofrece.</summary>
    internal async Task<AssistantActionEntry?> FindAsync(string key, IReadOnlyCollection<string> sealedKeys, string providerModelId,
        CancellationToken ct)
    {
        if (!sealedKeys.Contains(key, StringComparer.Ordinal)) return null;
        var action = (await ListAsync(providerModelId, ct)).SingleOrDefault(value => string.Equals(value.Key, key, StringComparison.Ordinal));
        return action is null ? null : _entries.Single(entry => ReferenceEquals(entry.Action, action));
    }

    /// <summary>
    /// Una acción <c>Company</c>: el permiso se mira en la empresa de la acción, nunca «en alguna» (0068 §3). Sin empresa
    /// elegida solo alcanza el permiso de organización que la ruta también acepta.
    /// </summary>
    internal async Task<bool> HoldsInCompanyAsync(AssistantActionDefinition action, Guid? companyId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId || _user.Access != Access.Business) return false;
        var granted = await _permissions.GetBusinessPermissionsAsync(userId, ct);
        if (companyId is null)
            return action.Requirement.OrganizationPermission is { } alternative && granted.Organization.Contains(alternative, StringComparer.Ordinal);
        return await HoldsAsync(userId, action.Requirement, granted, companyId, ct);
    }

    private static bool IsOfAccess(AssistantActionRequirementKind kind, Access access) => kind switch
    {
        AssistantActionRequirementKind.Consumer => access == Access.Consumer,
        AssistantActionRequirementKind.Organization or AssistantActionRequirementKind.Company => access == Access.Business,
        AssistantActionRequirementKind.Platform => access == Access.Platform,
        _ => false,
    };

    // La IA no tiene permisos propios: cuentan los efectivos de quien pide, con lo incluido (0068 §3, 260).
    private async Task<bool> HoldsAsync(Guid userId, AssistantActionRequirement requirement, BusinessPermissions? granted,
        Guid? companyId, CancellationToken ct)
    {
        switch (requirement.Kind)
        {
            case AssistantActionRequirementKind.Consumer:
                return true;
            case AssistantActionRequirementKind.Platform:
                return requirement.Permission is { } platformPermission && await _platform.HasPermissionAsync(userId, platformPermission, ct);
            case AssistantActionRequirementKind.Organization:
                return granted is not null && requirement.Permission is { } permission && granted.Organization.Contains(permission, StringComparer.Ordinal);
            case AssistantActionRequirementKind.Company:
                if (granted is null || requirement.Permission is not { } companyPermission) return false;
                if (requirement.OrganizationPermission is { } alternative && granted.Organization.Contains(alternative, StringComparer.Ordinal)) return true;
                // Al listar no hay empresa todavía: se ofrece a quien lo tiene en alguna, y proponer lo mira en la elegida.
                return companyId is { } company
                    ? await _permissions.HasCompanyPermissionAsync(granted, company, companyPermission, ct)
                    : granted.Organization.Contains(companyPermission, StringComparer.Ordinal)
                        || granted.ByCompany.Values.Any(values => values.Contains(companyPermission, StringComparer.Ordinal));
            default:
                return false;
        }
    }
}
