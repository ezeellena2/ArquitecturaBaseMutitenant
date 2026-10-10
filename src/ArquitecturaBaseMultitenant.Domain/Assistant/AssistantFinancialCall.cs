using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Espejo global por llamada, sólo tokens, tarifa histórica y costo. No guarda datos de chat.</summary>
public sealed class AssistantFinancialCall : Entity
{
    private AssistantFinancialCall() { }
    public Guid OperationId { get; private set; }
    public int Sequence { get; private set; }
    public bool IsTitle { get; private set; }
    public AssistantPriceSnapshot Price { get; private set; } = null!;
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public long CacheReadTokens { get; private set; }
    public long CacheWrite5MinutesTokens { get; private set; }
    public long CacheWrite1HourTokens { get; private set; }
    public long CacheWriteUnknownTokens { get; private set; }
    public long? ReasoningTokens { get; private set; }
    public bool IsEstimated { get; private set; }
    public bool IsFinal { get; private set; }
    public Money Cost { get; private set; } = null!;
    public static AssistantFinancialCall Create(Guid operationId, int sequence, bool isTitle, AssistantPriceSnapshot price,
        long input, long output, Money conservativeCost)
    {
        if (operationId == Guid.Empty || sequence < 1 || input < 0 || output < 0) throw new ArgumentException("A bounded call is required.");
        return new() { OperationId = operationId, Sequence = sequence, IsTitle = isTitle, Price = price,
            InputTokens = input, OutputTokens = output, Cost = conservativeCost, IsEstimated = true };
    }
    /// <summary>Reemplaza un update acumulativo; jamás suma como una llamada nueva.</summary>
    public void ReplaceUsage(long input, long output, long read, long write5, long write1, long writeUnknown,
        long? reasoning, bool estimated, Money cost)
    {
        if (IsFinal) return;
        if (input < 0 || output < 0 || read < 0 || write5 < 0 || write1 < 0 || writeUnknown < 0
            || read + write5 + write1 + writeUnknown > input || reasoning < 0 || reasoning > output)
            throw new ArgumentException("Usage counts are inconsistent.");
        InputTokens = input; OutputTokens = output; CacheReadTokens = read; CacheWrite5MinutesTokens = write5;
        CacheWrite1HourTokens = write1; CacheWriteUnknownTokens = writeUnknown; ReasoningTokens = reasoning;
        IsEstimated = estimated; Cost = cost;
    }
    public void FinalizeUsage() => IsFinal = true;
}
