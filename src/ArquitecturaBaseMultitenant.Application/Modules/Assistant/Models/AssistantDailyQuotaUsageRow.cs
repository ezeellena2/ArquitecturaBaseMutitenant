namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Uso diario del alcance de financiación; no anticipa la admisión de otro turno.</summary>
public sealed record AssistantDailyQuotaUsageRow(int ConsumedQueries, int ReservedQueries);
