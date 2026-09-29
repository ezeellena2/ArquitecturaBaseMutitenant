namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Únicas claims que describen el acceso activo de una sesión.</summary>
public static class TenantClaimTypes
{
    public const string Access = "access";
    public const string TenantId = "tenant_id";
    public const string TenantKind = "tenant_kind";
}
