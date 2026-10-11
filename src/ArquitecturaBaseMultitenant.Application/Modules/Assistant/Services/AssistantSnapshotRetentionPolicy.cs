using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Content expires at the original 24-hour boundary; a technical binding has no content lifetime.</summary>
internal static class AssistantSnapshotRetentionPolicy
{
    internal static bool HasContent(AssistantDurableOperationRow row, DateTime nowUtc) =>
        row.SnapshotJson is not null && row.CreatedAtUtc > nowUtc.AddHours(-24);
}
