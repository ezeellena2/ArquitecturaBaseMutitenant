namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

/// <summary>Preferencias por defecto del tenant activo.</summary>
public sealed record TenantSettingsRow(Guid TenantId, string DefaultCulture,
    string DefaultTimeZoneId, string DefaultCurrency);
