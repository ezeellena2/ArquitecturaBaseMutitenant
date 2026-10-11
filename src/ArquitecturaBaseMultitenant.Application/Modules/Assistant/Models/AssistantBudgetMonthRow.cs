namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Intervalo financiero persistido e inmutable, aun cuando cambie la zona vigente.</summary>
public sealed record AssistantBudgetMonthRow(Guid Id, DateOnly Month, string TimeZoneId, DateTime StartUtc, DateTime EndUtc);
