using ArquitecturaBaseMultitenant.Application.Models.Settings;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record UpdateOrganizationSettingsHttpRequest(string? Name, string? DefaultCulture,
    string? DefaultTimeZoneId, string? DefaultCurrency, uint? Version)
{
    public override string ToString() => nameof(UpdateOrganizationSettingsHttpRequest);
}

public static class OrganizationSettingsHttpMappings
{
    public static UpdateOrganizationSettingsRequest ToModel(this UpdateOrganizationSettingsHttpRequest request) =>
        new(request.Name, request.DefaultCulture, request.DefaultTimeZoneId, request.DefaultCurrency, request.Version);
}
