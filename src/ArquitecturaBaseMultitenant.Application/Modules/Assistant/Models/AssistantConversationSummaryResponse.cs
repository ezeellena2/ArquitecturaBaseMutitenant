namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantConversationSummaryResponse(Guid Id, string? Title, bool IsSaved, uint Version, DateTime CreatedAtUtc, DateTime LastActivityAtUtc)
{
    /// <summary>false si esta versión no puede leerla (ADR 0064 §6): sin título ni mensajes, y se puede borrar (B14).</summary>
    public bool Available { get; init; } = true;
}
