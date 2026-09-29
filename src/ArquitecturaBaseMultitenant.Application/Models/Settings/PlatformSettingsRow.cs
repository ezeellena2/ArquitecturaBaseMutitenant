using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Models.Settings;

/// <summary>Ajustes globales publicados a los casos de uso.</summary>
public sealed record PlatformSettingsRow(
    ConsumerSignupMode ConsumerSignup,
    BusinessSignupMode BusinessSignup,
    int MaxOwnedOrganizations,
    int AccountDeletionGraceDays);
