namespace ArquitecturaBaseMultitenant.Application.Models.Profile;

/// <summary>Permisos efectivos del acceso activo, separados entre organización, empresas y Plataforma.</summary>
public sealed record EffectivePermissions(IReadOnlyList<string> Organization,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> Companies, IReadOnlyList<string> Platform)
{
    public static EffectivePermissions Empty { get; } = new([], new Dictionary<Guid, IReadOnlyList<string>>(), []);
}
