namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantNoticeReader
{
    /// <summary>Trusted published notice revision. A request never selects the current revision.</summary>
    string CurrentVersion { get; }
    Task<bool> ExistsAsync(Guid ownerUserId, string noticeVersion, CancellationToken ct);
}
