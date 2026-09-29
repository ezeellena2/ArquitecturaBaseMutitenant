using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

internal sealed record GoogleAvailability(bool IsEnabled) : IGoogleAvailability;
