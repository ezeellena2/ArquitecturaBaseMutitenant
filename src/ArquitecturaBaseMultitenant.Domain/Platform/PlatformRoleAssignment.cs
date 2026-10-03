using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

/// <summary>Asigna un rol global a una cuenta de operador. Su versión se mapea a xmin.</summary>
public sealed class PlatformRoleAssignment : Entity, IVersioned
{
    private PlatformRoleAssignment()
    {
    }

    private PlatformRoleAssignment(Guid userId, PlatformRole role)
    {
        UserId = userId;
        Role = role;
    }

    public Guid UserId { get; private set; }

    public PlatformRole Role { get; private set; }

    public uint Version { get; private set; }

    public static PlatformRoleAssignment Create(Guid userId, PlatformRole role)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        return new PlatformRoleAssignment(userId, role);
    }

    public void ChangeRole(PlatformRole role)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        Role = role;
    }
}
