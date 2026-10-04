using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using System.Text;
using PermissionCatalog = ArquitecturaBaseMultitenant.Domain.Authorization.Permissions;

namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Permisos asignables dentro de una organización.</summary>
public sealed class Role : Entity, ITenantOwned, IAuditable, IVersioned
{
    private readonly List<string> _permissions = [];

    private Role()
    {
    }

    private Role(string name, string? description, RoleScope scope, Guid? companyId,
        IReadOnlyCollection<string> permissions, bool isSystemRole)
    {
        Name = name.Trim();
        NormalizedName = NormalizeName(name);
        Description = description;
        Scope = scope;
        CompanyId = companyId;
        IsSystemRole = isSystemRole;
        _permissions.AddRange(permissions);
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public RoleScope Scope { get; private set; }

    public Guid? CompanyId { get; private set; }

    public bool IsSystemRole { get; private set; }

    public IReadOnlyCollection<string> Permissions => _permissions.AsReadOnly();

    public Guid TenantId { get; private set; }

    public uint Version { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime? ModifiedAtUtc { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static Result<Role> Create(string? name, string? description, RoleScope scope, Guid? companyId,
        IReadOnlyCollection<string>? permissions)
    {
        var validation = Validate(name, description, scope, companyId, permissions, isSystemRole: false);
        if (validation.IsFailure) return validation.Error;
        if (SystemRoles.All.Any(systemName => NormalizeName(systemName) == NormalizeName(name!)))
            return RoleErrors.SystemRoleNameReserved;

        return new Role(name!, description, scope, companyId, permissions!, isSystemRole: false);
    }

    internal static Role CreateSystem(string name, RoleScope scope, IReadOnlyCollection<string> permissions)
    {
        if (!SystemRoles.All.Contains(name, StringComparer.Ordinal))
            throw new ArgumentException("The system role name is not registered.", nameof(name));

        var validation = Validate(name, description: null, scope, companyId: null, permissions, isSystemRole: true);
        if (validation.IsFailure)
            throw new InvalidOperationException("The registered system role definition is invalid.");

        return new Role(name, null, scope, companyId: null, permissions, isSystemRole: true);
    }

    public Result Update(string? name, string? description, RoleScope scope, Guid? companyId,
        IReadOnlyCollection<string>? permissions)
    {
        if (IsSystemRole) return RoleErrors.SystemRoleCannotChange;
        var validation = Validate(name, description, scope, companyId, permissions, isSystemRole: false);
        if (validation.IsFailure) return validation.Error;
        if (SystemRoles.All.Any(systemName => NormalizeName(systemName) == NormalizeName(name!)))
            return RoleErrors.SystemRoleNameReserved;

        Name = name!.Trim();
        NormalizedName = NormalizeName(name);
        Description = description;
        Scope = scope;
        CompanyId = companyId;
        _permissions.Clear();
        _permissions.AddRange(permissions!);
        return Result.Success();
    }

    /// <summary>
    /// Si el rol pasaría a valer en otro lugar. Con personas asignadas no puede (D2): sus asignaciones dejarían de
    /// valer en silencio.
    /// </summary>
    public bool ChangesScope(RoleScope scope, Guid? companyId) => Scope != scope || CompanyId != companyId;

    private static Result Validate(string? name, string? description, RoleScope scope, Guid? companyId,
        IReadOnlyCollection<string>? permissions, bool isSystemRole)
    {
        if (string.IsNullOrWhiteSpace(name)) return RoleErrors.NameRequired;
        if (name.Trim().Length > TextLimits.ShortName) return RoleErrors.NameTooLong;
        if (description?.Length > TextLimits.Description) return RoleErrors.DescriptionTooLong;
        if (!Enum.IsDefined(scope)) return RoleErrors.InvalidScope;
        if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));
        if (permissions is null) return RoleErrors.PermissionNotFound;

        if (scope is RoleScope.Organization or RoleScope.AnyCompany && companyId is not null)
            return RoleErrors.CompanyNotAllowed;
        if (scope == RoleScope.SpecificCompany && companyId is null)
            return RoleErrors.CompanyRequired;

        foreach (var permission in permissions)
        {
            if (!PermissionCatalog.All.Contains(permission, StringComparer.Ordinal)) return RoleErrors.PermissionNotFound;
            if (!isSystemRole && scope != RoleScope.Organization
                && !PermissionCatalog.CompanyScoped.Contains(permission, StringComparer.Ordinal))
                return RoleErrors.PermissionScopeInvalid;
        }

        return Result.Success();
    }

    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant().Normalize(NormalizationForm.FormC);
    }
}
