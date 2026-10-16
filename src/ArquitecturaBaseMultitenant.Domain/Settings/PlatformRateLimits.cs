namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>Una cuota: cuántos pedidos admite cada ventana fija UTC, sin cola. No hay cero para apagarla.</summary>
public sealed record RateLimitQuota(int PermitLimit, int WindowSeconds)
{
    public const int MinPermitLimit = 1;
    public const int MaxPermitLimit = 1_000_000;
    public const int MinWindowSeconds = 1;
    public const int MaxWindowSeconds = 86_400;

    public bool IsValid() => PermitLimit is >= MinPermitLimit and <= MaxPermitLimit
        && WindowSeconds is >= MinWindowSeconds and <= MaxWindowSeconds;
}

/// <summary>
/// Las doce cuotas compartidas que edita Plataforma con motivo (E9). <see cref="Initial"/> son los valores de
/// arranque (P3 moderados, y los cinco perfiles que ya existían con sus valores): sirven solo al crear la
/// configuración y cambiarlos no pisa lo guardado.
/// </summary>
public sealed record PlatformRateLimits(RateLimitQuota Ip, RateLimitQuota PublicIp, RateLimitQuota Identity,
    RateLimitQuota Tenant, RateLimitQuota LoginCode, RateLimitQuota LoginVerify, RateLimitQuota InvitationPreview,
    RateLimitQuota InvitationAccept, RateLimitQuota AssistantQuickQuery, RateLimitQuota AssistantConversation,
    RateLimitQuota AssistantNewKey, RateLimitQuota WhatsAppWebhook)
{
    public static PlatformRateLimits Initial { get; } = new(
        Ip: new(300, 60), PublicIp: new(60, 60), Identity: new(120, 60), Tenant: new(600, 60),
        LoginCode: new(20, 900), LoginVerify: new(30, 900), InvitationPreview: new(60, 900),
        InvitationAccept: new(10, 900), AssistantQuickQuery: new(20, 60), AssistantConversation: new(10, 60),
        AssistantNewKey: new(3, 900), WhatsAppWebhook: new(120, 60));

    public RateLimitQuota For(RateLimitProfile profile) => profile switch
    {
        RateLimitProfile.Ip => Ip,
        RateLimitProfile.PublicIp => PublicIp,
        RateLimitProfile.Identity => Identity,
        RateLimitProfile.Tenant => Tenant,
        RateLimitProfile.LoginCode => LoginCode,
        RateLimitProfile.LoginVerify => LoginVerify,
        RateLimitProfile.InvitationPreview => InvitationPreview,
        RateLimitProfile.InvitationAccept => InvitationAccept,
        RateLimitProfile.AssistantQuickQuery => AssistantQuickQuery,
        RateLimitProfile.AssistantConversation => AssistantConversation,
        RateLimitProfile.AssistantNewKey => AssistantNewKey,
        RateLimitProfile.WhatsAppWebhook => WhatsAppWebhook,
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };

    /// <summary>Los doce perfiles presentes y dentro de sus cotas.</summary>
    public bool IsValid() => Enum.GetValues<RateLimitProfile>().All(profile => For(profile) is { } quota && quota.IsValid());
}
