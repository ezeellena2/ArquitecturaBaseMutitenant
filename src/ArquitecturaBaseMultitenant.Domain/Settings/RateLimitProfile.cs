namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Los doce perfiles cerrados de cuotas compartidas (E9). Los cuatro primeros son generales: cada petición de la API cuenta
/// en los que le tocan. Los demás son de una ruta, que los declara con su metadata.
/// </summary>
public enum RateLimitProfile
{
    Ip,
    PublicIp,
    Identity,
    Tenant,
    LoginCode,
    LoginVerify,
    InvitationPreview,
    InvitationAccept,
    AssistantQuickQuery,
    AssistantConversation,
    AssistantNewKey,
    WhatsAppWebhook,
}

/// <summary>Lo que separa los pedidos de una cuota: la IP real, la identidad firmada o la organización del claim.</summary>
public enum RateLimitPartitionKind
{
    Ip,
    Identity,
    Tenant,
}

/// <summary>El nombre estable de cada perfil, el mismo del contrato, y la partición que usa.</summary>
public static class RateLimitProfiles
{
    public static string Name(RateLimitProfile profile) => profile switch
    {
        RateLimitProfile.Ip => "ip",
        RateLimitProfile.PublicIp => "publicIp",
        RateLimitProfile.Identity => "identity",
        RateLimitProfile.Tenant => "tenant",
        RateLimitProfile.LoginCode => "loginCode",
        RateLimitProfile.LoginVerify => "loginVerify",
        RateLimitProfile.InvitationPreview => "invitationPreview",
        RateLimitProfile.InvitationAccept => "invitationAccept",
        RateLimitProfile.AssistantQuickQuery => "assistantQuickQuery",
        RateLimitProfile.AssistantConversation => "assistantConversation",
        RateLimitProfile.AssistantNewKey => "assistantNewKey",
        RateLimitProfile.WhatsAppWebhook => "whatsAppWebhook",
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };

    /// <summary>Las cuotas del Asistente son de cada persona; las del ingreso y el webhook, de cada IP.</summary>
    public static RateLimitPartitionKind PartitionOf(RateLimitProfile profile) => profile switch
    {
        RateLimitProfile.Identity or RateLimitProfile.AssistantQuickQuery or RateLimitProfile.AssistantConversation
            or RateLimitProfile.AssistantNewKey => RateLimitPartitionKind.Identity,
        RateLimitProfile.Tenant => RateLimitPartitionKind.Tenant,
        _ when Enum.IsDefined(profile) => RateLimitPartitionKind.Ip,
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };
}
