using System.ComponentModel.DataAnnotations;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Security;

/// <summary>
/// Las cuotas de arranque (E9), en RateLimiting:&lt;Perfil&gt;. El seed las guarda sólo al crear la configuración de
/// Plataforma y el limitador las usa mientras esa configuración no existe; cambiarlas no pisa las cuotas guardadas, que
/// edita Plataforma con motivo.
/// </summary>
public sealed class RateLimitOptions : IValidatableObject
{
    public const string SectionName = "RateLimiting";

    public RateLimitQuotaOptions Ip { get; set; } = new(PlatformRateLimits.Initial.Ip);
    public RateLimitQuotaOptions PublicIp { get; set; } = new(PlatformRateLimits.Initial.PublicIp);
    public RateLimitQuotaOptions Identity { get; set; } = new(PlatformRateLimits.Initial.Identity);
    public RateLimitQuotaOptions Tenant { get; set; } = new(PlatformRateLimits.Initial.Tenant);
    public RateLimitQuotaOptions LoginCode { get; set; } = new(PlatformRateLimits.Initial.LoginCode);
    public RateLimitQuotaOptions LoginVerify { get; set; } = new(PlatformRateLimits.Initial.LoginVerify);
    public RateLimitQuotaOptions InvitationPreview { get; set; } = new(PlatformRateLimits.Initial.InvitationPreview);
    public RateLimitQuotaOptions InvitationAccept { get; set; } = new(PlatformRateLimits.Initial.InvitationAccept);
    public RateLimitQuotaOptions AssistantQuickQuery { get; set; } = new(PlatformRateLimits.Initial.AssistantQuickQuery);
    public RateLimitQuotaOptions AssistantConversation { get; set; } = new(PlatformRateLimits.Initial.AssistantConversation);
    public RateLimitQuotaOptions AssistantNewKey { get; set; } = new(PlatformRateLimits.Initial.AssistantNewKey);
    public RateLimitQuotaOptions WhatsAppWebhook { get; set; } = new(PlatformRateLimits.Initial.WhatsAppWebhook);

    public PlatformRateLimits ToLimits() => new(Ip.ToQuota(), PublicIp.ToQuota(), Identity.ToQuota(), Tenant.ToQuota(),
        LoginCode.ToQuota(), LoginVerify.ToQuota(), InvitationPreview.ToQuota(), InvitationAccept.ToQuota(),
        AssistantQuickQuery.ToQuota(), AssistantConversation.ToQuota(), AssistantNewKey.ToQuota(),
        WhatsAppWebhook.ToQuota());

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var limits = ToLimits();
        return Enum.GetValues<RateLimitProfile>()
            .Where(profile => !limits.For(profile).IsValid())
            .Select(profile => new ValidationResult(
                SectionName + ":" + profile + " needs 1 to 1000000 requests and a window of 1 to 86400 seconds.",
                [profile.ToString()]));
    }
}

/// <summary>Una cuota de arranque: pedidos por ventana fija UTC y la duración de la ventana en segundos.</summary>
public sealed class RateLimitQuotaOptions
{
    public RateLimitQuotaOptions() { }

    internal RateLimitQuotaOptions(RateLimitQuota quota)
    {
        PermitLimit = quota.PermitLimit;
        WindowSeconds = quota.WindowSeconds;
    }

    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public RateLimitQuota ToQuota() => new(PermitLimit, WindowSeconds);
}
