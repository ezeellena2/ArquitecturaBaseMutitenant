namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Distingue el espacio personal de una cuenta de una organización de empresa. Esta clasificación permite
/// asociar cada espacio al acceso correspondiente.
/// </summary>
public enum TenantKind
{
    Personal,
    Business,
}
