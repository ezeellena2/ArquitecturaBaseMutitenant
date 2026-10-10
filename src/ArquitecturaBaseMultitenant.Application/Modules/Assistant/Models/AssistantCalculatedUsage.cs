using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantCalculatedUsage(long InputTokens, long OutputTokens, long CacheReadTokens,
    long CacheWrite5MinutesTokens, long CacheWrite1HourTokens, long CacheWriteUnknownTokens,
    long? ReasoningTokens, bool IsEstimated, Money Cost);
