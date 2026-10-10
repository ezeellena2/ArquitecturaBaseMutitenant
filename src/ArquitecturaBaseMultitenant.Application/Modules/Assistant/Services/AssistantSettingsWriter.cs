using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsWriter(IAssistantSettingsRepository repository, IAssistantSettingsReader reader,
    AssistantSettingsBuilder builder, AssistantSettingsSelectionPolicy selection,
    IAssistantKeyProtector protector, AssistantSettingsAuditWriter audit)
{
    internal async Task<Result<AssistantOrganizationSettings>> UpdateOrganizationAsync(UpdateOrganizationAssistantSettingsRequest request, CancellationToken ct)
    {
        var row = await repository.GetOrganizationAsync(request.Version!.Value, ct);
        var options = await builder.BuildAsync(ct);
        var own = request.Mode == AssistantMode.OwnKey;
        if (own && !options.AllowOwnKey) return AssistantErrors.SettingsOwnKeyNotAllowed;
        if (own && !options.HasAlertRecipient) return AssistantErrors.SettingsAlertRecipientRequired;
        if (!own && (request.DailyMessageLimit is not null || request.MonthlyBudget is not null)) return AssistantErrors.SettingsPlatformLimitsReadOnly;
        if (!own && request.KeyAction == AssistantKeyAction.Replace) return AssistantErrors.SettingsKeyRejected;
        var eligible = AssistantSettingsSelectionPolicy.ValidateOrganization(request, options);
        if (eligible.IsFailure) return eligible.Error;
        if (own && request.KeyAction == AssistantKeyAction.Keep && row?.EncryptedApiKey is null) return AssistantErrors.SettingsNotConfigured;
        var platform = await reader.FindPlatformAsync(ct);
        var created = row is null;
        row ??= AssistantOrganizationSettings.Create();
        row.Update(request.Mode!.Value.ToString(), request.ModelId!.Value, request.Effort!.Value.ToString(),
            own ? request.DailyMessageLimit ?? row.DailyMessageLimit : row.DailyMessageLimit,
            own ? request.MonthlyBudget ?? row.MonthlyBudget : row.MonthlyBudget, platform.RetentionDays);
        if (request.KeyAction == AssistantKeyAction.Replace) row.ReplaceKey(protector.ProtectOrganization(request.ApiKey!), Suffix(request.ApiKey!));
        if (request.KeyAction == AssistantKeyAction.Clear) row.ReplaceKey(null, null);
        if (created) repository.Add(row); else repository.MarkUpdated(row);
        return row;
    }
    internal async Task<Result<AssistantPlatformSettings>> UpdatePlatformAsync(UpdatePlatformAssistantSettingsRequest request, CancellationToken ct)
    {
        var row = await repository.GetPlatformAsync(request.Version!.Value, ct);
        var allowedIds = request.AllowedModelIds!;
        var eligible = await selection.ValidatePlatformAsync(request, ct);
        if (eligible.IsFailure) return eligible.Error;
        var created = row is null;
        row ??= AssistantPlatformSettings.Create(false, request.MonthlyOrganizationBudget!);
        var changes = new SecurityEventChanges();
        changes.AddBoolean("assistant.personalEnabled", row.PersonalEnabled, request.PersonalEnabled);
        changes.AddCount("assistant.dailyMessageLimit", row.DailyMessageLimit, request.DailyMessageLimit);
        changes.AddCount("assistant.retentionDays", row.RetentionDays, request.RetentionDays);
        changes.AddEnum("assistant.effort", Enum.Parse<AssistantEffort>(row.Effort), request.Effort!.Value);
        changes.AddBoolean("assistant.modelSelectionChanged", false,
            row.DefaultModelId != request.DefaultModelId || !row.AllowedModelIds.Order().SequenceEqual(allowedIds.Order()));
        changes.AddBoolean("assistant.budgetChanged", false, row.MonthlyOrganizationBudget != request.MonthlyOrganizationBudget);
        changes.AddBoolean("assistant.keyChanged", false, request.KeyAction != AssistantKeyAction.Keep);
        row.Update(request.DefaultModelId!.Value, allowedIds.ToArray(), request.Effort.Value.ToString(),
            request.DailyMessageLimit!.Value, request.MonthlyOrganizationBudget!, request.PersonalEnabled!.Value, request.RetentionDays!.Value);
        if (request.KeyAction == AssistantKeyAction.Replace) row.ReplaceKey(protector.ProtectPlatform(request.ApiKey!), Suffix(request.ApiKey!));
        if (request.KeyAction == AssistantKeyAction.Clear) row.ReplaceKey(null, null);
        if (created) repository.Add(row); else repository.MarkUpdated(row);
        audit.RecordGlobal("Assistant platform settings updated", changes);
        return row;
    }
    private static string Suffix(string key) => key[^Math.Min(4, key.Length)..];
}
