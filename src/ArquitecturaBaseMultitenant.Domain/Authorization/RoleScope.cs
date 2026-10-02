namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Ámbito de un rol de organización.</summary>
public enum RoleScope
{
    Organization,
    AnyCompany,
    SpecificCompany,
}
