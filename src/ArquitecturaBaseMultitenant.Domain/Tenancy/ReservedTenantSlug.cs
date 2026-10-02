namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Nombres de subdominio que la plataforma no entrega a organizaciones.</summary>
public sealed class ReservedTenantSlug
{
    private ReservedTenantSlug() { }

    internal ReservedTenantSlug(string slug) => Slug = slug;

    public string Slug { get; private set; } = string.Empty;

    public static IReadOnlyList<ReservedTenantSlug> Defaults =>
    [
        new("www"),
        new("app"),
        new("api"),
        new("auth"),
        new("admin"),
        new("empresas"),
        new("plataforma"),
    ];
}
