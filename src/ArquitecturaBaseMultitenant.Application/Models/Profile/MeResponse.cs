using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Profile;

/// <summary>Cuenta, accesos y preferencias efectivas de la sesión actual.</summary>
public sealed record MeResponse(Guid Id, string? DisplayName, Email? Email, Access Access,
    Guid? ActiveTenantId, bool HasPersonalSpace, IReadOnlyList<OrganizationSummary> Organizations,
    EffectivePermissions EffectivePermissions, string Culture, string TimeZoneId, string? CurrencyCode,
    IReadOnlyList<string> Features)
{
    public uint Version { get; init; }
    public bool NeedsPersonalLoginMethod { get; init; }
    public IReadOnlyList<PendingLegalDocumentResponse> PendingLegalDocuments { get; init; } = [];
    public IReadOnlyList<string> Permissions => EffectivePermissions.Organization;
}
