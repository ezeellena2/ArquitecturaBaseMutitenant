namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// ADR 0068 §5: el alcance de solo lectura del asistente abre una transacción READ ONLY que siempre se deshace; no guarda
/// ni confirma (Only_unit_of_work_saves_the_ef_context lo sigue cuidando).
/// </summary>
public sealed partial class TransactionBoundaryTests
{
    static partial void ContributeAssistantTransactionOwners(HashSet<string> owners) =>
        owners.Add("ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantReadOnlyScope");
}
