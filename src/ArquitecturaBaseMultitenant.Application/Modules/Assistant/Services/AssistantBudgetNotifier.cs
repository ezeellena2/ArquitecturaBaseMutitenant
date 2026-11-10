using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Los avisos del 80 % y del tope del mes (D23): una vez por organización, financiación y mes, por el outbox.</summary>
internal sealed class AssistantBudgetNotifier(IAssistantBudgetAlertRepository alerts, IAssistantFinanceRepository finance,
    AssistantBudgetAlertNoticeBuilder notices, IOutbox outbox, IAssistantBudgetEmailRenderer renderer, TimeProvider clock)
{
    internal async Task StageAsync(AssistantFinancialOperation operation, CancellationToken ct)
    {
        if (operation.BudgetMonthId is not { } monthId || operation.MonthlyBudget is not { Amount: > 0 } budget) return;
        var used = new Money(await finance.GetCommittedCostAsync(monthId, operation.Funding, ct), budget.Currency);
        foreach (var threshold in new[] { AssistantTechnicalLimits.BudgetNearPercent, AssistantTechnicalLimits.BudgetReachedPercent })
        {
            if (used.Amount < budget.Amount * threshold / 100m) continue;
            var alert = await alerts.FindAsync(monthId, operation.Funding, threshold, ct);
            if (alert is null)
            {
                alert = AssistantBudgetAlert.Create(operation.ScopeTenantId!.Value, monthId, operation.Funding, threshold, used, budget);
                alerts.Add(alert);
            }
            await EnqueueAsync(alert, ct);
        }
    }
    internal async Task EnqueueAsync(AssistantBudgetAlert alert, CancellationToken ct)
    {
        if (alert.EnqueuedAtUtc is not null) return;
        var recipients = await notices.BuildAsync(alert, ct);
        if (recipients.Count == 0) return;
        foreach (var (userId, notice) in recipients)
            outbox.Enqueue(OutboxChannel.Email, JsonSerializer.Serialize(await renderer.RenderAsync(notice, ct)), userId);
        alert.MarkEnqueued(clock.GetUtcNow().UtcDateTime);
    }
}
