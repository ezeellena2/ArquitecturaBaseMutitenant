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
    public string[] AllowedCountries { get; init; } = ["AR"];
    [Range(1, 10000)] public int DailyAuthCodeLimit { get; init; } = 100;
    [Range(1, 3650)] public int MessageRetentionDays { get; init; } = 90;
    [Range(6, 300)] public int RetryDelaySeconds { get; init; } = 6;
    [Range(1, 3600)] public int InboundPollSeconds { get; init; } = 30;
    public bool ProcessInboundInBackground { get; init; } = true;
    public bool SendArgentineMobilesWithoutNine { get; init; }
    public WhatsAppTemplateOptions Templates { get; init; } = new();
}
internal sealed class WhatsAppTemplateOptions
{
    public string LoginCode { get; init; } = "codigo_ingreso";
    public string Invitation { get; init; } = "invitacion_organizacion";
}
