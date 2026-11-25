using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;

/// <summary>
/// Lo que ya cuenta contra el tope de un mes: lo reservado o en curso, más el costo de lo completado. Una sola suma para la
/// admisión de un turno (el repositorio, que le agrega lo de su UOW) y para mirar una subida antes de leerla (el reader).
/// </summary>
internal static class AssistantBudgetUsageQuery
{
    internal static async Task<decimal> SumAsync(ApplicationDbContext db, IQueryable<AssistantFinancialOperation> operations,
        CancellationToken ct)
    {
        var reserved = await operations.Where(row => row.State == AssistantFinancialState.Reserved || row.State == AssistantFinancialState.Invoked)
            .SumAsync(row => row.ReservedCost.Amount, ct);
        var completed = await (from call in db.Set<AssistantFinancialCall>().AsNoTracking()
                               join operation in operations.Where(row => row.State == AssistantFinancialState.Completed) on call.OperationId equals operation.Id
                               select call.Cost.Amount).SumAsync(ct);
        return reserved + completed;
    }
}
