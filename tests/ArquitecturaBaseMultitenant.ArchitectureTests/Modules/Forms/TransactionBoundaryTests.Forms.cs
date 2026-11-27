namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// C11 de la E12: la barrera de las operaciones de archivo de los catálogos se toma y se suelta en una conexión y una transacción
/// propias, como la reserva idempotente, fuera de la del servicio; no guarda el contexto de EF.
/// </summary>
public sealed partial class TransactionBoundaryTests
{
    static partial void ContributeFormsTransactionOwners(HashSet<string> owners) =>
        owners.Add("ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories.FormWorkLeaseStore");
}
