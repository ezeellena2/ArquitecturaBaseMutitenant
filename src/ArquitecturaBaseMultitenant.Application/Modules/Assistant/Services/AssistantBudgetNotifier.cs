using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantBudgetNotifier(IAssistantBudgetAlertRepository alerts, IAssistantFinanceRepository finance,
    ITenantAdminReader owners, IPlatformSettingsReader settings, IUserRepository users, UserCultureResolver cultures,
    IOutbox outbox, IAssistantBudgetEmailRenderer renderer, TimeProvider clock)
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
        var recipients = new List<(Guid? UserId, string Address, string Culture, string Zone)>();
        if (alert.Funding == AssistantKeyFunding.OwnKey)
        {
            foreach (var recipient in await owners.ListActiveVerifiedEmailRecipientsAsync(alert.OrganizationId, ct))
            {
                var account = await users.GetByIdAsync(recipient.UserId, ct);
                if (account is not null) recipients.Add((recipient.UserId, recipient.Email.Value, account.Culture, account.TimeZoneId));
            }
        }
        else if ((await settings.FindAsync(ct))?.SupportEmail is { } support)
        {
            var profile = await cultures.ResolveAsync(null, null, ct);
            recipients.Add((null, support.Value, profile.Culture.Name, TimeZoneInfo.Utc.Id));
        }
        if (recipients.Count == 0) return;
        foreach (var recipient in recipients)
        {
            var message = await renderer.RenderAsync(recipient.Address, alert.Threshold, alert.Used, alert.Budget,
                recipient.Culture, recipient.Zone, ct);
            outbox.Enqueue(OutboxChannel.Email, JsonSerializer.Serialize(message), recipient.UserId);
        }
        alert.MarkEnqueued(clock.GetUtcNow().UtcDateTime);
    }
}
