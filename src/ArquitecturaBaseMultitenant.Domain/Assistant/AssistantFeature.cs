namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public static class AssistantFeature
{
    public const string Key = "assistant";
    public const int DefaultDailyMessageLimit = 50;
    public const int DefaultRetentionDays = 30;

    /// <summary>Las conversaciones del Inicio se guardan de 1 a 30 días («Privacidad y retención» de Plataforma, 272).</summary>
    public const int MaxRetentionDays = 30;
}
