using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsWriter(IAssistantSettingsRepository repository, IAssistantSettingsReader reader,
    AssistantSettingsBuilder builder, AssistantSettingsSelectionPolicy selection, AssistantConnectionPolicy connection,
    IAssistantKeyProtector protector, AssistantSettingsAuditWriter audit, TimeProvider clock)
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
        // 0064 §6: la clave de un proveedor que esta versión no conoce no sirve a otro y nunca se manda: guardar sin tocarla la descarta.
        var discard = request.KeyAction == AssistantKeyAction.Keep && row?.EncryptedApiKey is not null && options.UnsupportedProvider;
        if (own && request.KeyAction == AssistantKeyAction.Keep && (row?.EncryptedApiKey is null || discard)) return AssistantErrors.SettingsNotConfigured;
        var platform = await reader.FindPlatformAsync(ct);
        var created = row is null;
        row ??= AssistantOrganizationSettings.Create();
        row.Update(request.Mode!.Value.ToString(), request.ModelId!.Value, request.Effort!.Value.ToString(),
            own ? request.DailyMessageLimit ?? row.DailyMessageLimit : row.DailyMessageLimit,
            own ? request.MonthlyBudget ?? row.MonthlyBudget : row.MonthlyBudget, platform.RetentionDays);
        if (request.KeyAction == AssistantKeyAction.Replace) row.ReplaceKey(protector.ProtectOrganization(request.ApiKey!), Suffix(request.ApiKey!));
        if (request.KeyAction == AssistantKeyAction.Clear || discard) row.ReplaceKey(null, null);
        // Quien guarda la configuración ya vio la falla: la próxima vuelve a quedar si el proveedor la repite (B13).
        (await repository.FindOrganizationIssueAsync(ct))?.Record(null, clock.GetUtcNow().UtcDateTime);
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
        // 0064 §6: como en la organización, guardar sin tocar la clave de un proveedor desconocido la descarta.
        var discard = request.KeyAction == AssistantKeyAction.Keep && row?.EncryptedApiKey is not null
            && await connection.IsUnsupportedAsync(row.DefaultModelId, ct);
        row ??= AssistantPlatformSettings.Create(false, request.MonthlyOrganizationBudget!);
        var changes = new SecurityEventChanges();
        changes.AddBoolean("assistant.personalEnabled", row.PersonalEnabled, request.PersonalEnabled);
        changes.AddCount("assistant.dailyMessageLimit", row.DailyMessageLimit, request.DailyMessageLimit);
        changes.AddCount("assistant.retentionDays", row.RetentionDays, request.RetentionDays);
        changes.AddEnum("assistant.effort", Enum.Parse<AssistantEffort>(row.Effort), request.Effort!.Value);
        changes.AddBoolean("assistant.modelSelectionChanged", false,
            row.DefaultModelId != request.DefaultModelId || !row.AllowedModelIds.Order().SequenceEqual(allowedIds.Order()));
        changes.AddBoolean("assistant.budgetChanged", false, row.MonthlyOrganizationBudget != request.MonthlyOrganizationBudget);
        changes.AddBoolean("assistant.keyChanged", false, request.KeyAction != AssistantKeyAction.Keep || discard);
        row.Update(request.DefaultModelId!.Value, allowedIds.ToArray(), request.Effort.Value.ToString(),
            request.DailyMessageLimit!.Value, request.MonthlyOrganizationBudget!, request.PersonalEnabled!.Value, request.RetentionDays!.Value);
        if (request.KeyAction == AssistantKeyAction.Replace) row.ReplaceKey(protector.ProtectPlatform(request.ApiKey!), Suffix(request.ApiKey!));
        if (request.KeyAction == AssistantKeyAction.Clear || discard) row.ReplaceKey(null, null);
        (await repository.FindPlatformIssueAsync(ct))?.Record(null, clock.GetUtcNow().UtcDateTime);
        if (created) repository.Add(row); else repository.MarkUpdated(row);
        audit.RecordGlobal("Assistant platform settings updated", changes);
        return row;
    }

    /// <summary>
    /// La falla del proveedor de quien paga (0064 §7, B13), con el lock de su configuración y en su propia fila: la configuración
    /// no cambia de versión, así nadie que la esté editando recibe un 409 por esto. Si alguien la guardó desde que el turno armó su
    /// cliente (<paramref name="settingsVersion"/> ya no es la de ahora), lo que respondió el proveedor es de la clave o el modelo de
    /// antes, y no se anota: guardar ya limpió la falla.
    /// </summary>
    internal async Task<Result> RecordProviderIssueAsync(AssistantMode funding, string? code, uint settingsVersion, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (funding == AssistantMode.OwnKey)
        {
            if (await repository.LockOrganizationVersionAsync(ct) != settingsVersion) return Result.Success();
            var organization = await repository.FindOrganizationIssueAsync(ct);
            if (organization is null && code is not null) repository.Add(organization = AssistantOrganizationProviderIssue.Create());
            organization?.Record(code, nowUtc);
            return Result.Success();
        }
        if (await repository.LockPlatformVersionAsync(ct) != settingsVersion) return Result.Success();
        var platform = await repository.FindPlatformIssueAsync(ct);
        if (platform is null && code is not null) repository.Add(platform = AssistantPlatformProviderIssue.Create());
        platform?.Record(code, nowUtc);
        return Result.Success();
    }
    private static string Suffix(string key) => key[^Math.Min(4, key.Length)..];
}
