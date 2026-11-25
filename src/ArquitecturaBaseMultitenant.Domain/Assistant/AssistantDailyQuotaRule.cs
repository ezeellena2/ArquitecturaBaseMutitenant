namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>
/// El cupo diario de consultas, con la clave de la plataforma o con la propia: entra una consulta más mientras lo reservado y lo
/// consumido no llegan al límite. La misma regla admite un turno y mira una subida antes de leerla (ADR 0064 §2).
/// </summary>
public static class AssistantDailyQuotaRule
{
    public static bool CanReserve(int reservedQueries, int consumedQueries, int limit) => (long)reservedQueries + consumedQueries < limit;
}
