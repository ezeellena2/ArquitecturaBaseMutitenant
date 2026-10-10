namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public static class AssistantTechnicalLimits
{
    public const int MaxInputTokens = 51200;
    public const int MaxOutputTokens = 2048;
    public const int TitleMaxOutputTokens = 64;
    // Includes the title call. Admission/reservations and the provider loop are supplied by T05/T06.
    public const int MaxCallsPerTurn = 5;
}
