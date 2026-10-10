namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Conteos técnicos acumulativos de UNA llamada. Input incluye read/write; reasoning está dentro de output.</summary>
public sealed record AssistantCallUsage(long? InputTokens, long? OutputTokens, long? CacheReadTokens,
    long? CacheWrite5MinutesTokens, long? CacheWrite1HourTokens, long? CacheWriteUnknownTokens,
    long? ReasoningTokens, bool CacheBreakdownComplete);
