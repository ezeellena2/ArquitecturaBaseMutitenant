namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

/// <summary>Receptor interno verificado; no es una proyección HTTP ni toma contactos de Google.</summary>
public sealed record ActiveTenantAdminEmail(Guid UserId, string Email)
{
    public override string ToString() => "ActiveTenantAdminEmail { [REDACTED] }";
}
