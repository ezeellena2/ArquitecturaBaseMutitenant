using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Copia privada de consumo por llamada; se confirma junto con el espejo técnico.</summary>
public sealed class AssistantUsageFact : Entity, ITenantOwned
{
    private AssistantUsageFact() { }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Access Access { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid CallId { get; private set; }
    public AssistantKeyFunding Funding { get; private set; }
    public DateOnly DayUtc { get; private set; }
    public Guid? BudgetMonthId { get; private set; }
    public Guid ModelId { get; private set; }
    public string PriceRevision { get; private set; } = string.Empty;
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public long CacheReadTokens { get; private set; }
    public long CacheWriteTokens { get; private set; }
    public bool IsTitle { get; private set; }
    public bool IsEstimated { get; private set; }
    public Money Cost { get; private set; } = null!;
    public static AssistantUsageFact Create(AssistantFinancialOperation operation, AssistantFinancialCall call)
    {
        if (operation.Access == Access.Platform || call.OperationId != operation.Id || !call.IsFinal)
            throw new ArgumentException("A completed private financial call is required.");
        return new() { UserId = operation.UserId, Access = operation.Access, OperationId = operation.Id, CallId = call.Id,
            Funding = operation.Funding, DayUtc = operation.DayUtc, BudgetMonthId = operation.BudgetMonthId,
            ModelId = call.Price.ModelId, PriceRevision = call.Price.Revision, InputTokens = call.InputTokens,
            OutputTokens = call.OutputTokens, CacheReadTokens = call.CacheReadTokens,
            CacheWriteTokens = call.CacheWrite5MinutesTokens + call.CacheWrite1HourTokens + call.CacheWriteUnknownTokens,
            IsTitle = call.IsTitle, IsEstimated = call.IsEstimated, Cost = new Money(call.Cost.Amount, call.Cost.Currency) };
    }
}
