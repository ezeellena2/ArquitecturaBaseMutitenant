using System.ComponentModel.DataAnnotations;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";
    public string? PhoneNumberId { get; init; }
    public string? BusinessAccountId { get; init; }
    public string? DisplayPhoneNumber { get; init; }
    public string? AccessToken { get; init; }
    public string? AppSecret { get; init; }
    public string? VerifyToken { get; init; }
    public bool HasWebhookSecrets => !string.IsNullOrWhiteSpace(AppSecret) && !string.IsNullOrWhiteSpace(VerifyToken);
    public string GraphApiVersion { get; init; } = "v25.0";
    public string[] AllowedCountries { get; set; } = ["AR"];
    [Range(1, 10000)] public int DailyAuthCodeLimit { get; init; } = 100;
    [Range(6, 300)] public int RetryDelaySeconds { get; init; } = 6;
    [Range(1, 3600)] public int InboundPollSeconds { get; init; } = 30;
    [Range(1, 3600)] public int InboundLeaseSeconds { get; init; } = 120;
    [Range(1, 100)] public int InboundMaxAttempts { get; init; } = 8;
    public bool ProcessInboundInBackground { get; init; } = true;
    public bool SendArgentineMobilesWithoutNine { get; init; }
    public WhatsAppTemplateOptions Templates { get; init; } = new();
}
internal sealed class WhatsAppTemplateOptions
{
    public string LoginCode { get; init; } = "codigo_ingreso";
    public string Invitation { get; init; } = "invitacion_organizacion";
    public string LoginMethodChanged { get; init; } = "aviso_metodo_ingreso";
    public string ReviewLoginMethods { get; init; } = "revisa_metodos_ingreso";
    public string DeletionRequested { get; init; } = "baja_cuenta_pedida";
    public string DeletionCancelled { get; init; } = "baja_cuenta_cancelada";
    public string AccountDeleted { get; init; } = "cuenta_eliminada";
    public string DeletionRequestedByPlatform { get; init; } = "baja_cuenta_plataforma";
    public string DeletionRequestedSuspended { get; init; } = "baja_cuenta_plataforma_suspendida";
    public string RecoveryReceived { get; init; } = "recuperacion_recibida";
    public string RecoveryApproved { get; init; } = "recuperacion_aprobada";
    public string RecoveryRejected { get; init; } = "recuperacion_rechazada";
    public string RecoveryRequested { get; init; } = "recuperacion_aviso_cuenta";
    public string OrganizationApproved { get; init; } = "organizacion_aprobada";
    public string OrganizationRejected { get; init; } = "organizacion_rechazada";
    public string PublicPageModerated { get; init; } = "pagina_publica_moderada";
}
