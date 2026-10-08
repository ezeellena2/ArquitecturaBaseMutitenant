using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
public sealed record LoginLinkSnapshot(Guid UserId, Guid MethodId, DateTime ExpiresAtUtc,
    DateTime? ConsumedAtUtc, DateTime? InvalidatedAtUtc, UserAccountRow Account, LoginMethodRow Method,
    bool LockoutEnabled, DateTime? LockoutEndUtc)
{
    public override string ToString() => nameof(LoginLinkSnapshot);
}
public interface ILoginLinkRepository
{
    Task<LoginLinkOwnerSnapshot?> FindOwnerAsync(Guid userId, Guid methodId, CancellationToken ct);
    Task<LoginLinkSnapshot?> FindAsync(string hash, CancellationToken ct);
    Task<LoginLink?> GetAsync(string hash, CancellationToken ct);
    Task<IReadOnlyList<LoginLink>> ListPendingAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<DateTime>> ListIssueTimesSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken ct);
    void Add(LoginLink link);
}
public sealed record LoginLinkOwnerSnapshot(UserAccountRow Account, LoginMethodRow Method, bool LockoutEnabled, DateTime? LockoutEndUtc)
{
    public override string ToString() => nameof(LoginLinkOwnerSnapshot);
}
