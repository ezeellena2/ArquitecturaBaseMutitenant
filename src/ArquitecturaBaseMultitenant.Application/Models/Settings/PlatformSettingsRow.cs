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
    PlatformRateLimits? RateLimits = null,
    int LoginAuditRetentionMonths = PlatformSettingsLimits.DefaultLoginAuditRetentionMonths,
    int LoginCodeRetentionDays = PlatformSettingsLimits.DefaultLoginCodeRetentionDays,
    int ClosedInvitationRetentionDays = PlatformSettingsLimits.DefaultClosedInvitationRetentionDays,
    string? AppName = null,
    string? SenderName = null,
    string? EmailLogoUrl = null);

/// <summary>
/// «Nombre y correos» de Plataforma (decisión 273) como lo usan los correos y las respuestas del bot: lo guardado o, lo que
/// todavía no se guardó, el respaldo de la configuración de arranque (<c>Email:AppName</c>, <c>Email:Smtp:FromName</c> y
/// <c>Email:LogoUrl</c>) hasta el despliegue siguiente (ADR 0060). Sin logo, null.
/// </summary>
public sealed record PlatformBrandingRow(string AppName, string SenderName, string? EmailLogoUrl);
