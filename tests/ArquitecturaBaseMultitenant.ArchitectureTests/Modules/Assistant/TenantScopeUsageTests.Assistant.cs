namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// ADR 0068 §5: el alcance de solo lectura del asistente copia en su scope propio la organización que fijó el acceso de la
/// petición; nunca entra con Enter.
/// </summary>
public sealed partial class TenantScopeUsageTests
{
    static partial void ContributeAssistantAccessInitializerCallers(HashSet<string> declared) =>
        declared.Add("ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantReadOnlyScope");
}
