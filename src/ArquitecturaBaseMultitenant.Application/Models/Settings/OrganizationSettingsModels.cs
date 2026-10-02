namespace ArquitecturaBaseMultitenant.Application.Models.Settings;

public sealed record UpdateOrganizationSettingsRequest(string? Name, string? DefaultCulture,
    string? DefaultTimeZoneId, string? DefaultCurrency, uint? Version);

public sealed record OrganizationSettingsResponse(string Name, string DefaultCulture,
    string DefaultTimeZoneId, string DefaultCurrency, uint Version);
