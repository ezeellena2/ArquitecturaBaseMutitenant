using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// Admisión financiera interna, dentro de la única UOW del turno. No concede autorización de ejecución. El cupo diario y el
/// tope mensual se miran acá también antes de leer una subida (<see cref="CheckAsync"/>, ADR 0064 §2), con el mismo día y el
/// mismo mes que admiten un turno.
/// </summary>
internal sealed class AssistantLimitGuard(IAssistantFinanceRepository repository, ITimeZoneService zones, AssistantQuotaDayResolver days,
    TimeProvider clock)
{
    public async Task<Result<AssistantFinancialOperation>> ReserveAsync(AssistantFinanceAdmission input, CancellationToken ct)
    {
        Validate(input);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var day = DayOf(input, nowUtc);
        await repository.LockAsync(input.Binding, input.OperationId, ct);
        // A durable released operation remains a replay, and never admits another provider effect.
        if (await repository.GetOperationAsync(input.Binding, input.OperationId, ct) is { } previous) return previous;
        var own = input.Mode == AssistantMode.OwnKey;
        var (platformQuota, ownQuota) = await QuotasAsync(day, input.Binding.UserId, own, ct);
        if (!(own ? ownQuota!.CanReserve(input.DailyLimit) : platformQuota!.CanReserve(input.DailyLimit)))
            return DailyReached(input.DailyLimit, nowUtc, day);
        var reserved = AssistantCostPolicy.ReserveTurn(input.Price, input.EligibleTitlePrice);
        // ResolveAsync carries the platform budget for ALL PK actors. Binding, never nullable Money, selects scope.
        var budget = input.Binding.Access == Access.Business ? input.ConfigurationBudget : null;
        AssistantBudgetMonth? month = null;
        IReadOnlyList<AssistantBudgetMonth> newMonths = [];
        if (input.Binding.Access == Access.Business)
        {
            if (!AssistantContractPolicy.IsValidBudget(budget)) return AssistantErrors.SettingsNotConfigured;
            (month, newMonths) = await CurrentMonthAsync(input, nowUtc, ct);
            if (await MonthlyRefusalAsync(month, own, reserved, budget!, nowUtc, ct) is { } refused) return refused;
        }
        foreach (var newMonth in newMonths) repository.Add(newMonth);
        if (own)
        {
            ownQuota!.Reserve();
            repository.Add(ownQuota);
        }
        else
        {
            platformQuota!.Reserve();
            repository.Add(platformQuota);
        }
        var operation = AssistantFinancialOperation.Create(input.OperationId, input.Binding,
            own ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey, day.Day, input.DailyLimit,
            month?.Id, budget, reserved, AssistantPriceSnapshotBuilder.Copy(input.Price),
            input.EligibleTitlePrice is null ? null : AssistantPriceSnapshotBuilder.Copy(input.EligibleTitlePrice), nowUtc);
        repository.Add(operation);
        return operation;
    }

    /// <summary>
    /// Lo que respondería la admisión de un turno con el cupo diario y el tope mensual, sin reservar, sin bloquear y sin
    /// escribir: la subida que no tendría turno no se lee ni se guarda (ADR 0064 §2). Sin un tope mensual válido no decide;
    /// el turno responde lo suyo.
    /// </summary>
    public async Task<Result> CheckAsync(AssistantFinanceAdmission input, CancellationToken ct)
    {
        Validate(input);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var day = DayOf(input, nowUtc);
        var own = input.Mode == AssistantMode.OwnKey;
        var (platformQuota, ownQuota) = await QuotasAsync(day, input.Binding.UserId, own, ct);
        if (!(own ? ownQuota!.CanReserve(input.DailyLimit) : platformQuota!.CanReserve(input.DailyLimit)))
            return DailyReached(input.DailyLimit, nowUtc, day);
        var budget = input.Binding.Access == Access.Business ? input.ConfigurationBudget : null;
        if (!AssistantContractPolicy.IsValidBudget(budget)) return Result.Success();
        var (month, _) = await CurrentMonthAsync(input, nowUtc, ct);
        var reserved = AssistantCostPolicy.ReserveTurn(input.Price, input.EligibleTitlePrice);
        return await MonthlyRefusalAsync(month, own, reserved, budget!, nowUtc, ct) is { } refused ? refused : Result.Success();
    }

    private static void Validate(AssistantFinanceAdmission input)
    {
        input.Binding.Validate();
        if (input.DailyLimit < 1 || !Enum.IsDefined(input.Mode) || input.Mode == AssistantMode.OwnKey && input.Binding.Access != Access.Business)
            throw new ArgumentException("Admission requires a valid server configuration.", nameof(input));
    }

    /// <summary>
    /// El día de los contadores diarios de IA, el mismo para un turno y para una subida (271): el de la zona de la organización
    /// en ella, con su cupo, y el de la zona del perfil fuera de una, que la preparación deja en la admisión.
    /// </summary>
    private AssistantQuotaDay DayOf(AssistantFinanceAdmission input, DateTime nowUtc) => days.Of(
        AssistantQuotaDayResolver.OrganizationOf(input.Binding.Access, input.Binding.ScopeTenantId), nowUtc, input.OrganizationTimeZoneId);

    private async Task<(AssistantPlatformKeyDailyQuota? Platform, AssistantOwnKeyDailyQuota? Own)> QuotasAsync(AssistantQuotaDay day,
        Guid userId, bool own, CancellationToken ct) => own
        ? (null, await repository.GetOwnQuotaAsync(userId, day.Day, ct) ?? AssistantOwnKeyDailyQuota.Create(userId, day.Day))
        : (await repository.GetPlatformQuotaAsync(userId, day.OrganizationId, day.Day, ct)
            ?? AssistantPlatformKeyDailyQuota.Create(userId, day.OrganizationId, day.Day), null);

    private static Error DailyReached(int limit, DateTime nowUtc, AssistantQuotaDay day) =>
        AssistantErrors.LimitDailyReached(RetrySeconds(nowUtc, day.ResetAtUtc), limit, day.ResetAtUtc);

    /// <summary>El período en curso de la organización y los que hay que abrir para llegar a él; solo la admisión los guarda.</summary>
    private async Task<(AssistantBudgetMonth Month, IReadOnlyList<AssistantBudgetMonth> Created)> CurrentMonthAsync(
        AssistantFinanceAdmission input, DateTime nowUtc, CancellationToken ct)
    {
        var created = new List<AssistantBudgetMonth>();
        var month = await repository.GetLatestMonthAsync(input.Binding.ScopeTenantId!.Value, nowUtc, ct);
        if (month is null)
        {
            var local = zones.ConvertToLocal(nowUtc, input.OrganizationTimeZoneId);
            var candidate = new DateOnly(local.Year, local.Month, 1);
            month = CreateMonth(input, candidate, zones.GetDayRangeUtc(candidate, input.OrganizationTimeZoneId).StartUtc);
            created.Add(month);
        }
        // Authorized transition: frozen periods stay intact; every next period starts at the previous end.
        // Empty elapsed months are materialized too, so a zone change never reopens a civil month or leaves a gap.
        while (nowUtc >= month.EndUtc)
        {
            month = CreateMonth(input, month.Month.AddMonths(1), month.EndUtc);
            created.Add(month);
        }
        return (month, created);
    }

    private async Task<Error?> MonthlyRefusalAsync(AssistantBudgetMonth month, bool own, Money reserved, Money budget, DateTime nowUtc,
        CancellationToken ct)
    {
        var committedAndReserved = await repository.GetCommittedAndReservedAmountAsync(month.Id,
            own ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey, ct);
        return committedAndReserved + reserved.Amount > budget.Amount
            ? AssistantErrors.LimitMonthlyReached(RetrySeconds(nowUtc, month.EndUtc)) : null;
    }

    private AssistantBudgetMonth CreateMonth(AssistantFinanceAdmission input, DateOnly month, DateTime startUtc) =>
        AssistantBudgetMonth.Create(input.Binding.ScopeTenantId!.Value, month, input.OrganizationTimeZoneId, startUtc,
            zones.GetDayRangeUtc(month.AddMonths(1), input.OrganizationTimeZoneId).StartUtc);
    public static AssistantDailyQuotaRow Describe(int consumed, int reserved, int limit, DateTime resetAtUtc)
    {
        var remaining = (int)Math.Max(0L, (long)limit - consumed - reserved);
        return new(consumed, reserved, limit, remaining > AssistantTechnicalLimits.RemainingMessagesNotice ? null : remaining, resetAtUtc);
    }
    private static int RetrySeconds(DateTime nowUtc, DateTime resetUtc) =>
        checked((int)Math.Max(1L, (resetUtc.Ticks - nowUtc.Ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond));
}
