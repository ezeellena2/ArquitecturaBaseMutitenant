namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Batch display metadata for an already authorized page. Accounts whose deletion has begun have no display metadata.</summary>
public interface IUserDisplayNameReader
{
    Task<IReadOnlyDictionary<Guid, string>> FindAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
