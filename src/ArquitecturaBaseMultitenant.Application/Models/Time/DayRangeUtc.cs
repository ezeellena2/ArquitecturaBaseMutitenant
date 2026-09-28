namespace ArquitecturaBaseMultitenant.Application.Models.Time;

/// <summary>Rango UTC de un día civil: inicio inclusivo y fin exclusivo.</summary>
public readonly record struct DayRangeUtc(DateTime StartUtc, DateTime EndUtc);
