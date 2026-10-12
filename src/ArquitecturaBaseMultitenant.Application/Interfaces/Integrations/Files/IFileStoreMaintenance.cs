namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;

/// <summary>Technical journals for a store write whose owning metadata may not have committed yet.</summary>
public interface IFileStoreMaintenance
{
    Task<IReadOnlyList<string>> ListUnconfirmedAsync(string category, DateTime cutoffUtc, CancellationToken ct);
    Task ConfirmAsync(string storageKey, string category, CancellationToken ct);
}
