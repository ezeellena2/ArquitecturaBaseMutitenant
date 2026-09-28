namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Esquemas de la base compartida.</summary>
public static class Schemas
{
    public const string Platform = "platform";
    public const string Identity = "identity";
    public const string Tenant = "tenant";
    public const string PublicSite = "public_site";
    public const string Engagement = "engagement";

    public static IReadOnlyList<string> All { get; } =
        [Platform, Identity, Tenant, PublicSite, Engagement];
}
