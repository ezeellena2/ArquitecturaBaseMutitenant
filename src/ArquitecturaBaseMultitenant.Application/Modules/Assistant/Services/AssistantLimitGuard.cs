using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Admisión financiera interna, dentro de la única UOW del turno. No concede autorización de ejecución.</summary>
internal sealed class AssistantLimitGuard(IAssistantFinanceRepository repository, ITimeZoneService zones, TimeProvider clock)
{
    public async Task<Result<AssistantFinancialOperation>> ReserveAsync(AssistantFinanceAdmission input, CancellationToken ct)
    {
        input.Binding.Validate();
        if (input.DailyLimit < 1 || !Enum.IsDefined(input.Mode) || input.Mode == AssistantMode.OwnKey && input.Binding.Access != Access.Business)
            throw new ArgumentException("Admission requires a valid server configuration.", nameof(input));
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var dayUtc = DateOnly.FromDateTime(nowUtc);
        await repository.LockAsync(input.Binding, input.OperationId, ct);
        // A durable released operation remains a replay, and never admits another provider effect.
        if (await repository.GetOperationAsync(input.Binding, input.OperationId, ct) is { } previous) return previous;
        var own = input.Mode == AssistantMode.OwnKey;
        var platformQuota = own ? null : await repository.GetPlatformQuotaAsync(input.Binding.UserId, dayUtc, ct)
            ?? AssistantPlatformKeyDailyQuota.Create(input.Binding.UserId, dayUtc);
        var ownQuota = own ? await repository.GetOwnQuotaAsync(input.Binding.UserId, dayUtc, ct)
            ?? AssistantOwnKeyDailyQuota.Create(input.Binding.UserId, dayUtc) : null;
        if (!(own ? ownQuota!.CanReserve(input.DailyLimit) : platformQuota!.CanReserve(input.DailyLimit)))
        {
            var resetUtc = dayUtc.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return AssistantErrors.LimitDailyReached(RetrySeconds(nowUtc, resetUtc), input.DailyLimit, resetUtc);
        }
        var reserved = AssistantCostPolicy.ReserveTurn(input.Price, input.EligibleTitlePrice);
        // ResolveAsync carries the platform budget for ALL PK actors. Binding, never nullable Money, selects scope.
        var budget = input.Binding.Access == Access.Business ? input.ConfigurationBudget : null;
        AssistantBudgetMonth? month = null;
        var newMonths = new List<AssistantBudgetMonth>();
        if (input.Binding.Access == Access.Business)
        {
            if (!AssistantContractPolicy.IsValidBudget(budget)) return AssistantErrors.SettingsNotConfigured;
            var local = zones.ConvertToLocal(nowUtc, input.OrganizationTimeZoneId);
            var candidate = new DateOnly(local.Year, local.Month, 1);
            month = await repository.GetLatestMonthAsync(input.Binding.ScopeTenantId!.Value, nowUtc, ct);
            if (month is null)
            {
                month = CreateMonth(input, candidate, zones.GetDayRangeUtc(candidate, input.OrganizationTimeZoneId).StartUtc);
                newMonths.Add(month);
            }
            // Authorized transition: frozen periods stay intact; every next period starts at the previous end.
            // Empty elapsed months are materialized too, so a zone change never reopens a civil month or leaves a gap.
            while (nowUtc >= month.EndUtc)
            {
                month = CreateMonth(input, month.Month.AddMonths(1), month.EndUtc);
                newMonths.Add(month);
            }
            var committedAndReserved = await repository.GetCommittedAndReservedAmountAsync(month.Id,
                own ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey, ct);
            if (committedAndReserved + reserved.Amount > budget!.Amount)
                return AssistantErrors.LimitMonthlyReached(RetrySeconds(nowUtc, month.EndUtc));
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
            own ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey, dayUtc, input.DailyLimit,
            month?.Id, budget, reserved, AssistantPriceSnapshotBuilder.Copy(input.Price),
            input.EligibleTitlePrice is null ? null : AssistantPriceSnapshotBuilder.Copy(input.EligibleTitlePrice), nowUtc);
        repository.Add(operation);
        return operation;
    }
    private AssistantBudgetMonth CreateMonth(AssistantFinanceAdmission input, DateOnly month, DateTime startUtc) =>
        AssistantBudgetMonth.Create(input.Binding.ScopeTenantId!.Value, month, input.OrganizationTimeZoneId, startUtc,
            zones.GetDayRangeUtc(month.AddMonths(1), input.OrganizationTimeZoneId).StartUtc);
    public static AssistantDailyQuotaRow Describe(int consumed, int reserved, int limit, DateOnly dayUtc)
    {
        var remaining = (int)Math.Max(0L, (long)limit - consumed - reserved);
        return new(consumed, reserved, limit, remaining > 10 ? null : remaining,
            dayUtc.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }
    private static int RetrySeconds(DateTime nowUtc, DateTime resetUtc) =>
        checked((int)Math.Max(1L, (resetUtc.Ticks - nowUtc.Ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond));
}
