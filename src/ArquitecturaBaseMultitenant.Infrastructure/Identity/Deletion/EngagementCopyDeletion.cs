using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

/// <summary>Adaptador del modelo EF compartido: conserva Parties y omite sólo SoftDelete para retirar toda copia.</summary>
internal sealed class EngagementCopyDeletion : IConsumerCopyDeletion
{
    private readonly ApplicationDbContext _database;
    private readonly ITenantContext _tenant;
    private readonly Dictionary<Type, IRetainedOnConsumerDeletion> _policies = [];

    public EngagementCopyDeletion(ApplicationDbContext database, ITenantContext tenant,
        IEnumerable<IRetainedOnConsumerDeletion> policies)
    {
        _database = database;
        _tenant = tenant;
        if (SharedTypes().Any(entity => !typeof(Entity).IsAssignableFrom(entity.ClrType)))
            throw new InvalidOperationException("A shared entity must derive from Entity for consumer deletion.");
        foreach (var policy in policies)
        {
            if (policy.RetentionPeriod <= TimeSpan.Zero || !IsReasonCode(policy.Reason)
                || !SharedTypes().Any(entity => entity.ClrType == policy.EntityType)
                || !_policies.TryAdd(policy.EntityType, policy))
                throw new InvalidOperationException("Consumer retention policies need one mapped shared type, a reason code and a positive period.");
        }

        if (SharedTypes().GroupBy(Key, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidOperationException("A shared table must identify exactly one root entity for consumer deletion.");
    }

    internal IEnumerable<IEntityType> SharedTypes() => _database.Model.GetEntityTypes()
        .Where(entity => entity.BaseType is null && !entity.IsOwned()
            && typeof(IConsumerBusinessShared).IsAssignableFrom(entity.ClrType));

    internal static string Key(IEntityType entity) =>
        entity.GetSchema() == Schemas.Engagement && entity.GetTableName() is { } table
            ? Schemas.Engagement + "." + table
            : throw new InvalidOperationException("Shared deletion requires an engagement table.");

    internal IRetainedOnConsumerDeletion? RetentionFor(Type entityType) => _policies.GetValueOrDefault(entityType);

    internal Task<IReadOnlyList<Entity>> ReadConsumerRowsAsync(Type entityType, Guid consumerTenantId,
        CancellationToken ct) => ReadAsync(entityType, consumerTenantId, null, null, ct);

    public async Task ReplaceAsync(string entityTypeKey, Guid entityId, Guid consumerTenantId,
        Guid businessTenantId, CancellationToken cancellationToken)
    {
        RequireScope(consumerTenantId);
        var entityType = SharedTypes().SingleOrDefault(entity => Key(entity) == entityTypeKey)
            ?? throw new InvalidOperationException("The retained shared table is no longer mapped.");
        var rows = await ReadAsync(entityType.ClrType, consumerTenantId, businessTenantId, entityId, cancellationToken);
        // Una fila que el módulo ya retiró tampoco tiene una copia para conservar.
        foreach (var row in rows) ((IConsumerBusinessShared)row).ReplaceConsumerCopy();
    }

    private Task<IReadOnlyList<Entity>> ReadAsync(Type entityType, Guid consumerTenantId,
        Guid? businessTenantId, Guid? entityId, CancellationToken ct)
    {
        RequireScope(consumerTenantId);
        var method = typeof(EngagementCopyDeletion).GetMethod(nameof(ReadTypedAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(entityType);
        var read = method.CreateDelegate<Func<Guid, Guid?, Guid?, CancellationToken, Task<IReadOnlyList<Entity>>>>(this);
        return read(consumerTenantId, businessTenantId, entityId, ct);
    }

    private async Task<IReadOnlyList<Entity>> ReadTypedAsync<T>(Guid consumerTenantId, Guid? businessTenantId,
        Guid? entityId, CancellationToken ct) where T : Entity, IConsumerBusinessShared
    {
        var rows = await _database.Set<T>().IgnoreQueryFilters(["SoftDelete"])
            .Where(row => row.ConsumerTenantId == consumerTenantId
                && (!businessTenantId.HasValue || row.BusinessTenantId == businessTenantId.Value)
                && (!entityId.HasValue || row.Id == entityId.Value)).ToArrayAsync(ct);
        return rows.Cast<Entity>().ToArray();
    }

    private void RequireScope(Guid consumerTenantId)
    {
        _database.RequireTransaction();
        if (_tenant.RequiredTenantId != consumerTenantId)
            throw new InvalidOperationException("Consumer copy deletion needs its personal tenant scope.");
    }

    private static bool IsReasonCode(string reason) => !string.IsNullOrWhiteSpace(reason)
        && reason.Length <= TextLimits.Description && reason.Split('.').Length >= 2
        && reason.Split('.').All(segment => segment.Length > 0 && char.IsAsciiLetter(segment[0])
            && segment.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'));
}
