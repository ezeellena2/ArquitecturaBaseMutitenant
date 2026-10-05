using System.Data.Common;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>Crea el rastro tenant en el mismo SaveChanges y habilita contrapartes compartidas en la transacción.</summary>
internal sealed class AuditTrailInterceptor(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    IRequestInfo requestInfo) : SaveChangesInterceptor
{
    private const string CounterpartSql =
        "SELECT set_config('app.audit_counterpart_tenant_ids', @tenant_ids, true)";

    /// <summary>Quién y cuándo ya están en la propia entrada; no se repiten en el diff.</summary>
    private static readonly HashSet<string> Stamps = new(StringComparer.Ordinal)
    {
        nameof(IAuditable.CreatedAtUtc), nameof(IAuditable.CreatedBy),
        nameof(IAuditable.ModifiedAtUtc), nameof(IAuditable.ModifiedBy),
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            var roleChanges = RoleAssignmentTrail.Collect(context);
            SetCounterparts(context, PrepareAuditEntries(context, roleChanges,
                RoleAssignmentTrail.LoadNames(context, roleChanges)));
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            var roleChanges = RoleAssignmentTrail.Collect(context);
            var roleNames = await RoleAssignmentTrail.LoadNamesAsync(context, roleChanges, cancellationToken);
            await SetCounterpartsAsync(context, PrepareAuditEntries(context, roleChanges, roleNames),
                cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Separado de la escritura SQL para probar el diff sin una conexión abierta.</summary>
    internal IReadOnlyCollection<Guid> PrepareAuditEntries(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var roleChanges = RoleAssignmentTrail.Collect(context);
        return PrepareAuditEntries(context, roleChanges, RoleAssignmentTrail.LoadNames(context, roleChanges));
    }

    private HashSet<Guid> PrepareAuditEntries(DbContext context,
        IReadOnlyCollection<RoleAssignmentTrail.RoleSetChange> roleChanges, IReadOnlyDictionary<Guid, string> roleNames)
    {
        RejectHistoryMutations(context);
        foreach (var entry in context.ChangeTracker.Entries<SecurityEvent>().Where(entry => entry.State == EntityState.Added))
            entry.Entity.WithRequestIp(requestInfo.IpAddress);
        var allChanged = context.ChangeTracker.Entries<IAuditable>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();
        RequireGlobalSecurityEvents(context, allChanged);
        // Las asignaciones de rol dejan un cambio por persona y lugar (RoleAssignmentTrail), no uno por asignación.
        var changed = allChanged.Where(entry => entry.Entity is not (PlatformSettings or TenantFeature or RoleAssignment))
            .ToArray();
        if (changed.Length == 0 && roleChanges.Count == 0)
        {
            return [];
        }

        var activeTenantId = tenantContext.RequiredTenantId;
        var (actorKind, actorId) = AuditActorResolver.Resolve(currentUser);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var counterparts = new HashSet<Guid>();
        // Invitar a alguien, revocar su invitación, agregarlo a una empresa o quitarlo de ella, o crear una empresa deja
        // una sola entrada.
        var access = AccessChangeTrail.Plan(changed, roleChanges);
        foreach (var change in access.StandaloneRoleChanges)
        {
            if (change.Place.TenantId != activeTenantId)
            {
                throw new InvalidOperationException("The audited entity does not belong to the active tenant.");
            }

            context.Add(AuditEntry.Create(activeTenantId, actorKind, actorId, AuditAction.Updated,
                nameof(RoleAssignment), change.Place.MemberId, JsonSerializer.Serialize(change.Diff(roleNames)), nowUtc));
        }

        foreach (var entry in changed)
        {
            var tenants = TenantIds(entry.Entity);
            if (!tenants.Contains(activeTenantId))
            {
                throw new InvalidOperationException("The audited entity does not belong to the active tenant.");
            }

            if (access.IsPartOfAnotherEntry(entry))
            {
                continue;
            }

            var diff = Diff(entry);
            // MarkUpdated sin ningún valor distinto (por ejemplo, para cambiar la versión) no es un cambio.
            if (entry.State == EntityState.Modified && diff.Count == 0)
            {
                continue;
            }

            access.Complete(entry, diff, roleNames);
            var action = Action(entry);
            var changes = JsonSerializer.Serialize(diff);
            var entityId = entry.Entity is Entity entity ? entity.Id :
                throw new InvalidOperationException("An auditable entity must have an Id.");
            foreach (var tenantId in tenants)
            {
                context.Add(AuditEntry.Create(tenantId, actorKind, actorId, action,
                    entry.Metadata.ClrType.Name, entityId, changes, nowUtc));
                if (tenantId != activeTenantId)
                {
                    counterparts.Add(tenantId);
                }
            }
        }

        return counterparts;
    }

    private static void RequireGlobalSecurityEvents(DbContext context,
        IReadOnlyCollection<EntityEntry<IAuditable>> changed)
    {
        foreach (var feature in changed.Select(entry => entry.Entity).OfType<TenantFeature>())
        {
            if (!context.ChangeTracker.Entries<SecurityEvent>().Any(entry => entry.State == EntityState.Added
                && entry.Entity.Type == SecurityEventType.TenantFeaturesChanged
                && entry.Entity.TargetTenantId == feature.TenantId && !string.IsNullOrWhiteSpace(entry.Entity.Reason)))
                throw new InvalidOperationException("Module changes require a security event with reason in the same transaction.");
        }
        if (!changed.Any(entry => entry.Entity is PlatformSettings))
        {
            return;
        }

        if (!context.ChangeTracker.Entries<SecurityEvent>().Any(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.Type == SecurityEventType.PlatformSettingsChanged &&
                !string.IsNullOrWhiteSpace(entry.Entity.Reason)))
        {
            throw new InvalidOperationException(
                "Global settings changes require a security event with reason in the same transaction.");
        }
    }

    private static void RejectHistoryMutations(DbContext context)
    {
        if (context.ChangeTracker.Entries<AuditEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Audit history is append-only.");
        }
    }

    private static IReadOnlyCollection<Guid> TenantIds(object entity) => entity switch
    {
        ITenantOwned privateData => [privateData.TenantId],
        IPublishedByBusiness publicData => [publicData.BusinessTenantId],
        IConsumerBusinessShared sharedData when sharedData.ConsumerTenantId == sharedData.BusinessTenantId =>
            [sharedData.ConsumerTenantId],
        IConsumerBusinessShared sharedData => [sharedData.ConsumerTenantId, sharedData.BusinessTenantId],
        _ => throw new InvalidOperationException("Global auditable data requires an explicit security event."),
    };

    private static AuditAction Action(EntityEntry<IAuditable> entry)
    {
        if (entry.State == EntityState.Added)
        {
            return AuditAction.Created;
        }

        if (entry.State == EntityState.Deleted)
        {
            return AuditAction.Deleted;
        }

        if (entry.Entity is ISoftDeletable)
        {
            var deleted = entry.Property(nameof(ISoftDeletable.IsDeleted));
            if (deleted.IsModified && deleted.OriginalValue is false && deleted.CurrentValue is true)
            {
                return AuditAction.Deleted;
            }

            if (deleted.IsModified && deleted.OriginalValue is true && deleted.CurrentValue is false)
            {
                return AuditAction.Restored;
            }
        }

        return AuditAction.Updated;
    }

    private static Dictionary<string, object?> Diff(EntityEntry<IAuditable> entry)
    {
        var changes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.PropertyInfo?.IsDefined(typeof(NotAuditedAttribute), true) == true ||
                Stamps.Contains(property.Metadata.Name) ||
                entry.State == EntityState.Modified && !Changed(property))
            {
                continue;
            }

            changes[property.Metadata.Name] = new Dictionary<string, object?>
            {
                ["old"] = entry.State == EntityState.Added ? null : SafeValue(property.Metadata.Name, property.OriginalValue),
                ["new"] = entry.State == EntityState.Deleted ? null : SafeValue(property.Metadata.Name, property.CurrentValue),
            };
        }

        return changes;
    }

    /// <summary>
    /// MarkUpdated marca la entidad entera: solo cuenta la propiedad cuyo valor cambió, comparado como lo compara EF.
    /// </summary>
    private static bool Changed(PropertyEntry property) =>
        property.IsModified && !property.Metadata.GetValueComparer().Equals(property.OriginalValue, property.CurrentValue);

    private static object? SafeValue(string propertyName, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return PersonalDataMask.Email(value.ToString() ?? string.Empty);
        }

        if (propertyName.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
            return PersonalDataMask.Phone(value.ToString() ?? string.Empty);
        }

        return value;
    }

    private static void SetCounterparts(DbContext context, HashSet<Guid> counterparts)
    {
        if (counterparts.Count == 0)
        {
            return;
        }

        using var command = CreateCounterpartCommand(context, counterparts);
        command.ExecuteScalar();
    }

    private static async Task SetCounterpartsAsync(
        DbContext context,
        HashSet<Guid> counterparts,
        CancellationToken cancellationToken)
    {
        if (counterparts.Count == 0)
        {
            return;
        }

        await using var command = CreateCounterpartCommand(context, counterparts);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static DbCommand CreateCounterpartCommand(DbContext context, HashSet<Guid> counterparts)
    {
        var transaction = context.Database.CurrentTransaction ??
            throw new InvalidOperationException("Shared audit entries require an active transaction.");
        var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = CounterpartSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_ids";
        parameter.Value = string.Join(',', counterparts.Order());
        command.Parameters.Add(parameter);
        return command;
    }
}
