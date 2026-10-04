using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

/// <summary>
/// Permisos individuales de Plataforma de una cuenta operadora, elegidos del catálogo. Hay una asignación por
/// cuenta y su versión se mapea a xmin.
/// </summary>
public sealed class PlatformPermissionAssignment : Entity, IVersioned
{
    private readonly List<string> _permissions = [];

    private PlatformPermissionAssignment()
    {
    }

    public Guid UserId { get; private set; }

    public IReadOnlyCollection<string> Permissions => _permissions.AsReadOnly();

    public uint Version { get; private set; }

    public static Result<PlatformPermissionAssignment> Create(Guid userId, IReadOnlyCollection<string>? permissions)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        var validation = Validate(permissions);
        if (validation.IsFailure) return validation.Error;

        var assignment = new PlatformPermissionAssignment { UserId = userId };
        assignment._permissions.AddRange(InCatalogOrder(permissions!));
        return assignment;
    }

    public Result ChangePermissions(IReadOnlyCollection<string>? permissions)
    {
        var validation = Validate(permissions);
        if (validation.IsFailure) return validation;

        _permissions.Clear();
        _permissions.AddRange(InCatalogOrder(permissions!));
        return Result.Success();
    }

    public bool Grants(string permission) => _permissions.Contains(permission, StringComparer.Ordinal);

    /// <summary>Al menos un permiso, todos del catálogo y sin repetir.</summary>
    public static Result Validate(IReadOnlyCollection<string>? permissions)
    {
        if (permissions is null || permissions.Count == 0) return PlatformOperatorErrors.PermissionsRequired;
        if (permissions.Any(permission => !PlatformPermissions.All.Contains(permission, StringComparer.Ordinal)))
            return PlatformOperatorErrors.PermissionNotFound;
        if (permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Count)
            return PlatformOperatorErrors.PermissionRepeated;
        return Result.Success();
    }

    private static IEnumerable<string> InCatalogOrder(IReadOnlyCollection<string> permissions) =>
        PlatformPermissions.All.Where(permission => permissions.Contains(permission, StringComparer.Ordinal));
}
