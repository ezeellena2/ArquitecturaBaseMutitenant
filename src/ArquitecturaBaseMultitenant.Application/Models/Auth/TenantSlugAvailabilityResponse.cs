namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

public sealed record TenantSlugAvailabilityResponse(string Slug, TenantSlugAvailability Availability);
