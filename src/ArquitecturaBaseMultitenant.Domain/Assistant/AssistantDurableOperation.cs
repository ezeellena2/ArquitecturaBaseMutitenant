using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Private committed binding. Content is removed on conversation deletion; the technical key survives.</summary>
public sealed class AssistantDurableOperation : AssistantDurableOperationBase, ITenantOwned
{
    private AssistantDurableOperation() { }
    public Guid TenantId { get; private set; }
    public Access Access { get; private set; }

    public static AssistantDurableOperation Create(Guid owner, Access access, Guid key, string hash, string route,
        Guid conversationId, string kind, int statusCode, string? location, string snapshot, string? quickKey, DateTime nowUtc,
        Guid[]? subjectUserIds = null)
    {
        if (access is not (Access.Consumer or Access.Business)) throw new ArgumentOutOfRangeException(nameof(access));
        var row = new AssistantDurableOperation { Access = access };
        row.Initialize(owner, key, hash, route, conversationId, kind, statusCode, location, snapshot, quickKey, nowUtc, subjectUserIds);
        return row;
    }
}
