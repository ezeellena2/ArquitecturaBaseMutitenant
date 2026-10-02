namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Plantillas inmutables instanciadas en cada organización.</summary>
public static class SystemRoles
{
    public const string TenantAdmin = "TenantAdmin";
    public const string CompanyAdmin = "CompanyAdmin";

    public static IReadOnlyCollection<string> All { get; } = [TenantAdmin, CompanyAdmin];

    public static IReadOnlyCollection<Role> CreateDefaults() => [CreateTenantAdmin(), CreateCompanyAdmin()];

    public static Role CreateTenantAdmin() => Role.CreateSystem(TenantAdmin, RoleScope.Organization, Permissions.All);

    public static Role CreateCompanyAdmin() => Role.CreateSystem(CompanyAdmin, RoleScope.AnyCompany, Permissions.CompanyScoped);
}
