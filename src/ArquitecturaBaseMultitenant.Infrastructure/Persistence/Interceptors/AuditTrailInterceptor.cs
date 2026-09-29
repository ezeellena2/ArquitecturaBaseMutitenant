using System.Data.Common;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
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
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    private const string CounterpartSql =
        "SELECT set_config('app.audit_counterpart_tenant_ids', @tenant_ids, true)";

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            SetCounterparts(context, PrepareAuditEntries(context));
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
            await SetCounterpartsAsync(context, PrepareAuditEntries(context), cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Separado de la escritura SQL para probar el diff sin una conexión abierta.</summary>
    internal IReadOnlyCollection<Guid> PrepareAuditEntries(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RejectHistoryMutations(context);
        var allChanged = context.ChangeTracker.Entries<IAuditable>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();
        RequireGlobalSecurityEvents(context, allChanged);
        var changed = allChanged.Where(entry => entry.Entity is not PlatformSettings).ToArray();
        if (changed.Length == 0)
        {
            return [];
        }

        var activeTenantId = tenantContext.RequiredTenantId;
        var (actorKind, actorId) = AuditActorResolver.Resolve(currentUser);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var counterparts = new HashSet<Guid>();
        foreach (var entry in changed)
        {
            var tenants = TenantIds(entry.Entity);
            if (!tenants.Contains(activeTenantId))
            {
                throw new InvalidOperationException("The audited entity does not belong to the active tenant.");
            }

            var action = Action(entry);
            var changes = JsonSerializer.Serialize(Diff(entry));
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
                entry.State == EntityState.Modified && !property.IsModified)
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

    private static object? SafeValue(string propertyName, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            var text = value.ToString() ?? string.Empty;
            var at = text.LastIndexOf('@');
            return at < 0 ? "***" : "***" + text[at..];
        }

        if (propertyName.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new string((value.ToString() ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            return digits.Length < 4 ? "***" : "***" + digits[^4..];
        }

        return value;
    }

    private static void SetCounterparts(DbContext context, IReadOnlyCollection<Guid> counterparts)
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
        IReadOnlyCollection<Guid> counterparts,
        CancellationToken cancellationToken)
    {
        if (counterparts.Count == 0)
        {
            return;
        }

        await using var command = CreateCounterpartCommand(context, counterparts);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static DbCommand CreateCounterpartCommand(DbContext context, IReadOnlyCollection<Guid> counterparts)
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
