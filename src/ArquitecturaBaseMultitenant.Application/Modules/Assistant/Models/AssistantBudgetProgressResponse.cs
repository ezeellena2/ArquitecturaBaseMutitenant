namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Registered monthly cost divided by the budget snapshot; reservations are excluded and the fraction is not capped.</summary>
public sealed record AssistantBudgetProgressResponse(decimal Fraction, AssistantBudgetStatus Status);
