namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

public sealed record RoleScopeCounts(int All, int Organization, int Company);

/// <summary>Una lectura para menú y resumen; una empresa sin roles propios comparte el conteo AnyCompany.</summary>
public sealed record RoleFilterCountsResponse(RoleScopeCounts Matching, RoleScopeCounts Total,
    int AnyCompany, IReadOnlyDictionary<Guid, int> Companies);
