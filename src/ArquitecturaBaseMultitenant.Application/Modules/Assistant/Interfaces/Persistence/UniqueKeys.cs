namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

/// <summary>Índices propios del módulo, compartidos por EF y sus casos de uso.</summary>
public static class UniqueKeys
{
    public const string AssistantActiveTurn = "IX_AssistantTurns_TenantId_ConversationId_Active";
    public const string PlatformAssistantActiveTurn = "IX_PlatformAssistantTurns_ConversationId_Active";
    public const string AssistantModel = "IX_AssistantModelPrices_Provider_ProviderModelId";
}
