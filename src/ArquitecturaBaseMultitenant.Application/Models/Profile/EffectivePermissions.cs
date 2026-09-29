namespace ArquitecturaBaseMultitenant.Application.Models.Profile;

/// <summary>Permisos efectivos del acceso activo; los permisos por empresa llegan en E4–E6.</summary>
public sealed record EffectivePermissions(IReadOnlyList<string> Organization,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> Companies)
{
    public static EffectivePermissions Empty { get; } = new([], new Dictionary<Guid, IReadOnlyList<string>>());
}
