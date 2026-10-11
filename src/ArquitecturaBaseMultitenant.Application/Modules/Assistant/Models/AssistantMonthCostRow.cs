using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Presupuesto snapshot y costo de operaciones completadas de una misma financiación.</summary>
public sealed record AssistantMonthCostRow(Money? MonthlyBudget, Money? UsedCost);
