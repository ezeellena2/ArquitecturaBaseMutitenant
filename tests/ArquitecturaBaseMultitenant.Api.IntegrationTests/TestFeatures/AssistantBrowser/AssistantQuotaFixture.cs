using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

internal static class AssistantQuotaFixture
{
    internal sealed record Staged(Guid OperationId, Guid[] Alerts);
    internal static async Task<Staged> StageAsync(IServiceProvider services, AssistantFinancialBinding binding,
        JsonElement args, Guid previousFixtureId, CancellationToken ct)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var day = DateOnly.FromDateTime(nowUtc);
        var remaining = args.GetProperty("remainingMessages").GetInt32();
        var fraction = args.GetProperty("monthlyFraction").GetDecimal();
        var alertIds = new List<Guid>();
        var operationId = Guid.Empty;
        await AssistantFileBridge.WriteAsync(services, async token =>
        {
            var platform = await db.Set<AssistantPlatformSettings>().SingleAsync(token);
            var own = binding.Access == Access.Business ? await db.Set<AssistantOrganizationSettings>().SingleOrDefaultAsync(token) : null;
            var ownKey = own?.Mode == "OwnKey";
            var funding = ownKey ? AssistantKeyFunding.OwnKey : AssistantKeyFunding.PlatformKey;
            var limit = ownKey ? own!.DailyMessageLimit : platform.DailyMessageLimit;
            if (remaining > limit) throw new InvalidOperationException("Fixture remaining exceeds current configured quota.");
            if (ownKey)
            {
                var quota = await db.Set<AssistantOwnKeyDailyQuota>().SingleOrDefaultAsync(row => row.UserId == binding.UserId && row.DayUtc == day, token);
                if (quota is null) { quota = AssistantOwnKeyDailyQuota.Create(binding.UserId, day); db.Add(quota); }
                if (quota.ReservedQueries != 0) throw new InvalidOperationException("Fixture cannot overwrite active reservations.");
                db.Entry(quota).Property(row => row.ConsumedQueries).CurrentValue = limit - remaining;
            }
            else
            {
                var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleOrDefaultAsync(row => row.UserId == binding.UserId && row.DayUtc == day, token);
                if (quota is null) { quota = AssistantPlatformKeyDailyQuota.Create(binding.UserId, day); db.Add(quota); }
                if (quota.ReservedQueries != 0) throw new InvalidOperationException("Fixture cannot overwrite active reservations.");
                db.Entry(quota).Property(row => row.ConsumedQueries).CurrentValue = limit - remaining;
            }
            if (binding.Access != Access.Business) return;
            // Sólo se sustituye el registro sintético creado por este mismo bridge; nunca consumos del navegador.
            if (previousFixtureId != Guid.Empty)
            {
                db.RemoveRange(await db.Set<AssistantUsageFact>().Where(row => row.OperationId == previousFixtureId).ToArrayAsync(token));
                db.RemoveRange(await db.Set<AssistantFinancialCall>().Where(row => row.OperationId == previousFixtureId).ToArrayAsync(token));
                db.RemoveRange(await db.Set<AssistantFinancialOperation>().Where(row => row.Id == previousFixtureId).ToArrayAsync(token));
            }
            var finance = services.GetRequiredService<IAssistantFinanceRepository>();
            var month = await finance.GetLatestMonthAsync(binding.ScopeTenantId!.Value, nowUtc, token);
            if (month is null)
            {
                var setting = await services.GetRequiredService<ITenantSettingsReader>().FindCurrentAsync(token)
                    ?? throw new InvalidOperationException("Scoped organization settings required.");
                var zones = services.GetRequiredService<ITimeZoneService>();
                var local = zones.ConvertToLocal(nowUtc, setting.DefaultTimeZoneId);
                var first = new DateOnly(local.Year, local.Month, 1);
                month = AssistantBudgetMonth.Create(binding.ScopeTenantId.Value, first, setting.DefaultTimeZoneId,
                    zones.GetDayRangeUtc(first, setting.DefaultTimeZoneId).StartUtc, zones.GetDayRangeUtc(first.AddMonths(1), setting.DefaultTimeZoneId).StartUtc);
                db.Add(month);
            }
            var budget = ownKey ? own!.MonthlyBudget : platform.MonthlyOrganizationBudget;
            if (budget is null || budget.Amount <= 0) throw new InvalidOperationException("Fixture requires an applicable real budget.");
            var model = await db.Set<AssistantModelPrice>().SingleAsync(row => row.Id == (ownKey ? own!.ModelId : platform.DefaultModelId), token);
            var price = AssistantPriceSnapshot.Create(model.Id, model.ProviderModelId, "isolated-budget-fixture", model.InputPrice,
                model.OutputPrice, model.InputPrice, model.InputPrice, model.InputPrice, null, null, null, null, null, null);
            var otherOperations = db.Set<AssistantFinancialOperation>().Where(row => row.BudgetMonthId == month.Id && row.Funding == funding
                && row.State == AssistantFinancialState.Completed && row.Id != previousFixtureId);
            var committed = await (from call in db.Set<AssistantFinancialCall>() join recordedOperation in otherOperations on call.OperationId equals recordedOperation.Id
                select call.Cost.Amount).SumAsync(token);
            var cost = AssistantFileBridge.Usd(Math.Max(0m, budget.Amount * fraction - committed));
            operationId = Guid.NewGuid();
            // Los snapshots y Money son owned de cada fila; no se comparten con el catálogo ni entre agregados.
            var operation = AssistantFinancialOperation.Create(operationId, binding, funding, day, limit, month.Id,
                new Money(budget.Amount, budget.Currency), AssistantFileBridge.Usd(0m), AssistantPriceSnapshotBuilder.Copy(price), null, nowUtc);
            operation.MarkInvoked(); operation.Complete(nowUtc);
            var callFixture = AssistantFinancialCall.Create(operationId, 1, false, AssistantPriceSnapshotBuilder.Copy(price), 0, 0, cost);
            callFixture.ReplaceUsage(0, 0, 0, 0, 0, 0, null, true, cost);
            callFixture.FinalizeUsage();
            db.AddRange(operation, callFixture, AssistantUsageFact.Create(operation, callFixture));
            if (args.TryGetProperty("notify", out var notify) && notify.GetBoolean())
                foreach (var threshold in new[] { 80, 100 }.Where(value => fraction * 100m >= value))
                {
                    var alert = await db.Set<AssistantBudgetAlert>().SingleOrDefaultAsync(row => row.MonthId == month.Id
                        && row.Funding == funding && row.Threshold == threshold, token);
                    if (alert is null) { alert = AssistantBudgetAlert.Create(binding.ScopeTenantId.Value, month.Id, funding, threshold,
                        AssistantFileBridge.Usd(budget.Amount * fraction), new Money(budget.Amount, budget.Currency)); db.Add(alert); }
                    alertIds.Add(alert.Id);
                }
        }, ct);
        return new(operationId, [.. alertIds]);
    }
}
