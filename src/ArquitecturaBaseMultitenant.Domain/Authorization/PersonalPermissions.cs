namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Permisos personales implícitos; cada módulo B2C agrega los propios cuando los necesite.</summary>
public static class PersonalPermissions
{
    public static IReadOnlyCollection<string> All { get; } = [];
}
