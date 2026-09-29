using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Profile;

/// <summary>Cuenta, accesos y preferencias efectivas de la sesión actual.</summary>
public sealed record MeResponse(Guid Id, string? DisplayName, string? Email, Access Access,
    Guid? ActiveTenantId, bool HasPersonalSpace, IReadOnlyList<OrganizationSummary> Organizations,
    EffectivePermissions EffectivePermissions, string Culture, string TimeZoneId, string? CurrencyCode,
    IReadOnlyList<string> Features)
{
    public IReadOnlyList<string> Permissions => EffectivePermissions.Organization;
}
