using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Private committed binding. Content is removed on conversation deletion; the technical key survives.</summary>
public sealed class AssistantDurableOperation : Entity, ITenantOwned, IVersioned
{
    private AssistantDurableOperation() { }
    public Guid TenantId { get; private set; }
    public uint Version { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Access Access { get; private set; }
    public Guid Key { get; private set; }
    public string BodyHash { get; private set; } = string.Empty;
    public string Route { get; private set; } = string.Empty;
    public Guid ConversationId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string? Location { get; private set; }
    public string? SnapshotJson { get; private set; }
    public string? QuickKey { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static AssistantDurableOperation Create(Guid owner, Access access, Guid key, string hash, string route,
        Guid conversationId, string kind, int statusCode, string? location, string snapshot, string? quickKey, DateTime nowUtc)
    {
        if (owner == Guid.Empty || key == Guid.Empty || conversationId == Guid.Empty) throw new ArgumentException("Non-empty binding IDs are required.");
        if (access is not (Access.Consumer or Access.Business)) throw new ArgumentOutOfRangeException(nameof(access));
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        return new() { OwnerUserId = owner, Access = access, Key = key, BodyHash = hash, Route = route,
            ConversationId = conversationId, Kind = kind, StatusCode = statusCode, Location = location,
            SnapshotJson = snapshot, QuickKey = quickKey, CreatedAtUtc = nowUtc };
    }

    public void RemoveContent() { SnapshotJson = null; QuickKey = null; }
}
