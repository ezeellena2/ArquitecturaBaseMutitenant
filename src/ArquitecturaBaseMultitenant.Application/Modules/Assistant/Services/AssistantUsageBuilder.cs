using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Services.Features;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantUsageBuilder(IAssistantUsageReader reader, IAssistantSettingsReader settings,
    IUserDisplayNameReader names, DisplayFormatter formatter, AssistantQuotaDayResolver days, TimeProvider clock,
    ModuleLimitResolver limits, ITenantContext tenant)
{
    internal async Task<AssistantUsagePageResponse> BuildAsync(string month, AssistantUsageReadPage page, bool business, CancellationToken cancellationToken)
    {
        var personIds = page.Rows.Items.Where(value => value.CanShowPersonName && value.PersonId != null)
            .Select(value => value.PersonId!.Value).Distinct().ToArray();
        var displayNames = personIds.Length == 0 ? new Dictionary<Guid, string>()
            : await names.FindAsync(personIds, cancellationToken);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var currentPeople = business && page.IsCurrentMonth
            ? page.Rows.Items.Where(value => value.Kind == AssistantUsageRowKind.Person && value.PersonId != null)
                .Select(value => value.PersonId!.Value).Distinct().ToArray() : [];
        var mode = AssistantMode.PlatformKey;
        var dailyLimit = 0;
        var daily = new Dictionary<Guid, AssistantPersonDailyUsageRow>();
        AssistantQuotaDay? quotaDay = null;
        if (currentPeople.Length > 0)
        {
            var configuration = await settings.FindOrganizationAsync(cancellationToken);
            mode = configuration?.Mode ?? AssistantMode.PlatformKey;
            // 0069 §4: con la clave de la plataforma, el cupo del día de la organización del claim sale del resolvedor.
            dailyLimit = mode == AssistantMode.OwnKey ? configuration!.DailyMessageLimit
                : (await limits.ResolveAsync(AssistantModuleLimitContributor.DailyMessagesKey, tenant.TenantId, cancellationToken)).Effective(null);
            // 271: el día de los cupos de la organización es el de su zona, para todos sus miembros.
            quotaDay = days.Of(null, nowUtc, await days.ZoneAsync(Domain.Users.Access.Business, TimeZoneInfo.Utc.Id, cancellationToken));
            daily = (await reader.ListDailyAsync(currentPeople, mode, quotaDay.Day, cancellationToken)).ToDictionary(value => value.UserId);
        }
        var format = page.Rows.Items.Any(value => value.Day != null)
            ? await formatter.CreateForCurrentCultureAsync(TimeZoneInfo.Utc.Id, cancellationToken) : null;
        var items = page.Rows.Items.Select(value =>
        {
            var displayName = value.Kind switch
            {
                AssistantUsageRowKind.Organization => value.OrganizationName ?? AssistantTexts.Get("Assistant.Usage.OrganizationUnavailable"),
                AssistantUsageRowKind.Person when value.CanShowPersonName && value.PersonId is { } id && displayNames.TryGetValue(id, out var name) => name,
                AssistantUsageRowKind.Person => AssistantTexts.Get("Assistant.Usage.PersonUnavailable"),
                AssistantUsageRowKind.Day => formatter.FormatDate(value.Day!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), format!),
                AssistantUsageRowKind.Personal => AssistantTexts.Get("Assistant.Usage.Personal"),
                _ => AssistantTexts.Get("Assistant.Usage.Operators")
            };
            AssistantDailyUsageResponse? quota = null;
            if (currentPeople.Length > 0 && value.PersonId is { } person)
            {
                daily.TryGetValue(person, out var usage);
                var consumed = usage?.ConsumedQueries ?? 0;
                var reserved = usage?.ReservedQueries ?? 0;
                quota = new(quotaDay!.Day, quotaDay.ResetAtUtc,
                    mode, consumed, dailyLimit, (long)consumed + reserved >= dailyLimit);
            }
            return new AssistantUsageRowResponse(value.Kind, value.OrganizationId, value.PersonId, value.Day,
                displayName, value.Queries, value.InputTokens, value.OutputTokens, value.CacheWriteTokens,
                value.CacheReadTokens, value.Cost, quota?.Limit ?? value.DailyLimit, value.MonthlyBudget, quota,
                value.PeopleCount, value.FundingMode, business ? null : Progress(value.Cost, value.MonthlyBudget));
        }).ToArray();
        return new(month, page.Cost, page.PlatformKeyCost, page.Budget, page.TotalQueries, page.PeopleCount,
            new PagedResult<AssistantUsageRowResponse>(items, page.Rows.Page, page.Rows.PageSize, page.Rows.TotalCount),
            Progress(page.Cost, page.Budget), page.OrganizationCount);
    }

    private static AssistantBudgetProgressResponse? Progress(Money cost, Money? budget) =>
        budget is { Amount: > 0 } ? AssistantStateBuilder.DescribeBudget(cost, budget) : null;
}
