namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Los plazos de retención del núcleo (decisión 272) y el nombre de los correos (decisión 273) que edita Plataforma: su
/// valor por defecto, su mínimo y su máximo. Sin valor guardado, valen los de defecto.
/// </summary>
public static class PlatformSettingsLimits
{
    public const int DefaultLoginAuditRetentionMonths = 12;
    public const int MinLoginAuditRetentionMonths = 1;
    public const int MaxLoginAuditRetentionMonths = 120;

    public const int DefaultLoginCodeRetentionDays = 30;
    public const int MinLoginCodeRetentionDays = 1;
    public const int MaxLoginCodeRetentionDays = 3650;

    public const int DefaultClosedInvitationRetentionDays = 30;
    public const int MinClosedInvitationRetentionDays = 7;
    public const int MaxClosedInvitationRetentionDays = 3650;

    /// <summary>El nombre de la aplicación y el del remitente, en los correos y las respuestas del bot.</summary>
    public const int NameMaxLength = 60;
}
