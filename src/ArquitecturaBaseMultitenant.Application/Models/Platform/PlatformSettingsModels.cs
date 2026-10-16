using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

public sealed record PlatformSettingsResponse(ConsumerSignupMode ConsumerSignup, BusinessSignupMode BusinessSignup,
    int MaxOwnedOrganizations, Email? SalesEmail, Email? SupportEmail, uint Version, PlatformRateLimitsResponse RateLimits);

/// <summary>Sin <see cref="RateLimits"/> se conservan las cuotas guardadas; con ellas, van los doce perfiles completos.</summary>
public sealed record UpdatePlatformSettingsRequest(ConsumerSignupMode? ConsumerSignup, BusinessSignupMode? BusinessSignup,
    int? MaxOwnedOrganizations, Email? SalesEmail, Email? SupportEmail, uint? Version, string? Reason,
    PlatformRateLimitsInput? RateLimits = null)
{
    public override string ToString() => nameof(UpdatePlatformSettingsRequest);
}

/// <summary>Los correos de contacto que muestran Ingreso y Registro, sin sesión; null mientras no se cargaron.</summary>
public sealed record PlatformContactResponse(Email? SalesEmail, Email? SupportEmail);

/// <summary>Una cuota (E9): pedidos admitidos por cada ventana fija UTC de <see cref="WindowSeconds"/> segundos.</summary>
public sealed record RateLimitQuotaResponse(int PermitLimit, int WindowSeconds)
{
    public static RateLimitQuotaResponse From(RateLimitQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);
        return new RateLimitQuotaResponse(quota.PermitLimit, quota.WindowSeconds);
    }
}

/// <summary>Los doce perfiles cerrados de cuotas compartidas que edita Plataforma (E9).</summary>
public sealed record PlatformRateLimitsResponse(RateLimitQuotaResponse Ip, RateLimitQuotaResponse PublicIp,
    RateLimitQuotaResponse Identity, RateLimitQuotaResponse Tenant, RateLimitQuotaResponse LoginCode,
    RateLimitQuotaResponse LoginVerify, RateLimitQuotaResponse InvitationPreview, RateLimitQuotaResponse InvitationAccept,
    RateLimitQuotaResponse AssistantQuickQuery, RateLimitQuotaResponse AssistantConversation,
    RateLimitQuotaResponse AssistantNewKey, RateLimitQuotaResponse WhatsAppWebhook)
{
    public static PlatformRateLimitsResponse From(PlatformRateLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return new PlatformRateLimitsResponse(RateLimitQuotaResponse.From(limits.Ip), RateLimitQuotaResponse.From(limits.PublicIp),
            RateLimitQuotaResponse.From(limits.Identity), RateLimitQuotaResponse.From(limits.Tenant),
            RateLimitQuotaResponse.From(limits.LoginCode), RateLimitQuotaResponse.From(limits.LoginVerify),
            RateLimitQuotaResponse.From(limits.InvitationPreview), RateLimitQuotaResponse.From(limits.InvitationAccept),
            RateLimitQuotaResponse.From(limits.AssistantQuickQuery), RateLimitQuotaResponse.From(limits.AssistantConversation),
            RateLimitQuotaResponse.From(limits.AssistantNewKey), RateLimitQuotaResponse.From(limits.WhatsAppWebhook));
    }
}

/// <summary>Una cuota editada: los dos valores son obligatorios, entre 1 y 1.000.000 pedidos y entre 1 y 86.400 segundos.</summary>
public sealed record RateLimitQuotaInput(int? PermitLimit, int? WindowSeconds);

/// <summary>Los doce perfiles editados; si llegan, llegan todos.</summary>
public sealed record PlatformRateLimitsInput(RateLimitQuotaInput? Ip, RateLimitQuotaInput? PublicIp,
    RateLimitQuotaInput? Identity, RateLimitQuotaInput? Tenant, RateLimitQuotaInput? LoginCode,
    RateLimitQuotaInput? LoginVerify, RateLimitQuotaInput? InvitationPreview, RateLimitQuotaInput? InvitationAccept,
    RateLimitQuotaInput? AssistantQuickQuery, RateLimitQuotaInput? AssistantConversation,
    RateLimitQuotaInput? AssistantNewKey, RateLimitQuotaInput? WhatsAppWebhook);
