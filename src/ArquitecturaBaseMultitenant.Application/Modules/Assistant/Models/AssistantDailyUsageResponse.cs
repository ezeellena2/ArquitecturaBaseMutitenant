namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Consumed UTC-day queries; admission exhaustion also includes reservations. PlatformKey is shared by user, OwnKey by organization and user.</summary>
public sealed record AssistantDailyUsageResponse(DateOnly DayUtc, DateTime ResetAtUtc, AssistantMode Scope, int Queries, int Limit, bool LimitReached);
