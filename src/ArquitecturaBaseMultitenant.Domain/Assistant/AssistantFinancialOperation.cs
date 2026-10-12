using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Reserva duradera técnica. Su identidad no es una conversación ni permite leer contenido.</summary>
public sealed class AssistantFinancialOperation : Entity
{
    private AssistantFinancialOperation() { }
    private AssistantFinancialOperation(Guid id) : base(id) { }
    public Guid UserId { get; private set; }
    public Access Access { get; private set; }
    public Guid? ScopeTenantId { get; private set; }
    public AssistantKeyFunding Funding { get; private set; }
    public DateOnly DayUtc { get; private set; }
    public int DailyLimit { get; private set; }
    public Guid? BudgetMonthId { get; private set; }
    public Money? MonthlyBudget { get; private set; }
    public Money ReservedCost { get; private set; } = null!;
    public AssistantPriceSnapshot Price { get; private set; } = null!;
    public AssistantPriceSnapshot? TitlePrice { get; private set; }
    public AssistantFinancialState State { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public bool ContentDeleted { get; private set; }
    public static AssistantFinancialOperation Create(Guid id, AssistantFinancialBinding binding, AssistantKeyFunding funding,
        DateOnly dayUtc, int dailyLimit, Guid? budgetMonthId, Money? budget, Money reservation,
        AssistantPriceSnapshot price, AssistantPriceSnapshot? titlePrice, DateTime nowUtc)
    {
        binding.Validate();
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(reservation);
        if (dailyLimit < 1 || !Enum.IsDefined(funding) || nowUtc.Kind != DateTimeKind.Utc
            || funding == AssistantKeyFunding.OwnKey && binding.Access != Access.Business
            || binding.Access != Access.Business && (budget is not null || budgetMonthId is not null)
            || (budget is null) != (budgetMonthId is null) || reservation.Amount < 0
            || reservation.Round(12).Amount != reservation.Amount || !reservation.Currency.Equals(price.Input.Currency))
            throw new ArgumentException("The financial reservation must come from a valid server configuration.");
        return new(id) { UserId = binding.UserId, Access = binding.Access, ScopeTenantId = binding.ScopeTenantId, Funding = funding,
            DayUtc = dayUtc, DailyLimit = dailyLimit, BudgetMonthId = budgetMonthId, MonthlyBudget = budget, ReservedCost = reservation,
            Price = price, TitlePrice = titlePrice, CreatedAtUtc = nowUtc, State = AssistantFinancialState.Reserved };
    }
    public bool IsBoundTo(AssistantFinancialBinding binding) => UserId == binding.UserId && Access == binding.Access && ScopeTenantId == binding.ScopeTenantId;
    public void MarkInvoked()
    {
        if (State != AssistantFinancialState.Reserved) throw new InvalidOperationException("A released or completed reservation cannot invoke a provider.");
        State = AssistantFinancialState.Invoked;
    }
    public void Complete(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        if (State is AssistantFinancialState.Completed or AssistantFinancialState.Released) return;
        State = State == AssistantFinancialState.Invoked ? AssistantFinancialState.Completed : AssistantFinancialState.Released;
        CompletedAtUtc = nowUtc;
    }
    /// <summary>El participante deja sólo contabilidad pseudónima; no se recrea una copia privada tras la baja.</summary>
    public void MarkContentDeleted() => ContentDeleted = true;
    public void Anonymize(Guid accountingIdentity)
    {
        if (accountingIdentity == Guid.Empty || State is not (AssistantFinancialState.Completed or AssistantFinancialState.Released) || !ContentDeleted)
            throw new InvalidOperationException("Only finalized deleted content can lose its personal binding.");
        UserId = accountingIdentity;
        if (Access != Access.Business) ScopeTenantId = null;
    }
}
