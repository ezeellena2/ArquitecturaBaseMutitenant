using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Settings;

/// <summary>Ajustes globales publicados a los casos de uso, con las cuotas compartidas guardadas (E9).</summary>
public sealed record PlatformSettingsRow(
    ConsumerSignupMode ConsumerSignup,
    BusinessSignupMode BusinessSignup,
    int MaxOwnedOrganizations,
    int AccountDeletionGraceDays,
    uint Version = 0,
    Email? SalesEmail = null,
    Email? SupportEmail = null,
    PlatformRateLimits? RateLimits = null);
