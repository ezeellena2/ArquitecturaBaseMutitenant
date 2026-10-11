using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantStateBuilder(AssistantNoticePolicy notices, ICurrentUser user,
    AssistantStateConfigurationBuilder configuration, AssistantToolResolver tools, IAssistantFinanceReader finance, TimeProvider clock)
{
    internal async Task<Result<AssistantStateResponse>> BuildAsync(CancellationToken ct)
    {
        var version = notices.CurrentVersion;
        var authorized = await notices.CheckVersionAsync(version, ct);
        if (authorized.IsFailure) return authorized.Error;
        var owner = user.UserId!.Value;
        var accepted = await notices.IsAcceptedAsync(owner, version, ct);
        var current = await configuration.BuildAsync(ct);
        var quick = new List<AssistantQuickQueryResponse>();
        if (accepted)
            foreach (var query in AssistantQuickCatalog.All.Where(query => query.Access == user.Access))
                if (await tools.IsAvailableAsync(query.ToolKey, ct)) quick.Add(new(query.Key, query.LabelKey));
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var dayUtc = DateOnly.FromDateTime(nowUtc);
        var usage = await finance.FindDailyQuotaAsync(owner, current.Mode, dayUtc, ct);
        var quota = AssistantLimitGuard.Describe(usage.ConsumedQueries, usage.ReservedQueries, current.DailyLimit, dayUtc);
        var (budget, cost) = user.Access == Access.Business
            ? await ReadMonthAsync(current, nowUtc, ct) : (null, null);
        var progress = budget is { Amount: > 0 } && cost is not null
            ? DescribeBudget(cost, budget) : null;
        // Recheck live authorization and consent after every projection; GET never records acceptance.
        authorized = await notices.CheckVersionAsync(version, ct);
        if (authorized.IsFailure) return authorized.Error;
        accepted = await notices.IsAcceptedAsync(owner, version, ct);
        authorized = await notices.CheckVersionAsync(version, ct);
        if (authorized.IsFailure) return authorized.Error;
        return new AssistantStateResponse(current.Configured, current.CanConfigure, current.Models,
            accepted ? quick : [], quota.Limit, quota.ResetAtUtc, quota.Remaining, budget, cost,
            version, accepted, current.RetentionDays, 10485760, 5, progress);
    }

    private async Task<(Money? Budget, Money? Cost)> ReadMonthAsync(AssistantStateConfigurationRow current, DateTime nowUtc, CancellationToken ct)
    {
        var month = await finance.FindLatestMonthAsync(nowUtc, ct);
        if (month is not null && nowUtc < month.EndUtc)
        {
            var funding = current.Mode == AssistantMode.OwnKey ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey;
            var recorded = await finance.FindMonthCostAsync(month.Id, funding, ct);
            if (recorded.MonthlyBudget is not null) return (recorded.MonthlyBudget, recorded.UsedCost);
        }
        // Admission owns the consecutive zone transition. With no opened current period there is no recorded cost;
        // GET must neither reopen the expired snapshot nor materialize empty intermediate periods.
        if (!current.Configured || current.MonthlyBudget is null) return (null, null);
        return (current.MonthlyBudget, new Money(0m, current.MonthlyBudget.Currency));
    }

    internal static AssistantBudgetProgressResponse DescribeBudget(Money cost, Money budget)
    {
        if (cost.Currency != budget.Currency) throw new InvalidOperationException("State requires one budget currency.");
        var fraction = cost.Amount / budget.Amount;
        return new(fraction, fraction >= 1m ? AssistantBudgetStatus.LimitReached
            : fraction >= 0.8m ? AssistantBudgetStatus.NearLimit : AssistantBudgetStatus.WithinLimit);
    }
}
