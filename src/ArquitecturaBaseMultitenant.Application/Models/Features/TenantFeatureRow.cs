using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Models.Features;

/// <summary>Datos persistidos sin cachear el resultado temporal de la habilitación.</summary>
public sealed record TenantFeatureRow(string Key, bool Enabled, TenantFeatureSource Source,
    DateTime? ExpiresAtUtc, uint Version)
{
    public bool IsEnabledAt(DateTime nowUtc) => Enabled && (ExpiresAtUtc is null || nowUtc < ExpiresAtUtc);
}
