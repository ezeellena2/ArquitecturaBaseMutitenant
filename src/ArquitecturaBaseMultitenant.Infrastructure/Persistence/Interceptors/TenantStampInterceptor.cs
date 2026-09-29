using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>Sella la columna de la parte activa y rechaza cambios de pertenencia.</summary>
internal sealed class TenantStampInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                StampAdded(entry);
            }
            else if (entry.State == EntityState.Modified)
            {
                RejectTenantChanges(entry);
            }
        }
    }

    private void StampAdded(EntityEntry entry)
    {
        if (entry.Entity is ITenantOwned)
        {
            entry.Property(nameof(ITenantOwned.TenantId)).CurrentValue = tenantContext.RequiredTenantId;
        }
        else if (entry.Entity is IPublishedByBusiness)
        {
            if (tenantContext.TenantKind != TenantKind.Business)
            {
                throw new InvalidOperationException("Public data requires an active business tenant.");
            }

            entry.Property(nameof(IPublishedByBusiness.BusinessTenantId)).CurrentValue = tenantContext.RequiredTenantId;
        }
        else if (entry.Entity is IConsumerBusinessShared)
        {
            var column = tenantContext.TenantKind switch
            {
                TenantKind.Personal => nameof(IConsumerBusinessShared.ConsumerTenantId),
                TenantKind.Business => nameof(IConsumerBusinessShared.BusinessTenantId),
                _ => throw new InvalidOperationException("Shared data requires a classified active tenant."),
            };
            entry.Property(column).CurrentValue = tenantContext.RequiredTenantId;
        }
    }

    private static void RejectTenantChanges(EntityEntry entry)
    {
        if (entry.Entity is ITenantOwned)
        {
            RejectChanged(entry, nameof(ITenantOwned.TenantId));
        }
        else if (entry.Entity is IPublishedByBusiness)
        {
            RejectChanged(entry, nameof(IPublishedByBusiness.BusinessTenantId));
        }
        else if (entry.Entity is IConsumerBusinessShared)
        {
            RejectChanged(entry, nameof(IConsumerBusinessShared.ConsumerTenantId));
            RejectChanged(entry, nameof(IConsumerBusinessShared.BusinessTenantId));
        }
    }

    private static void RejectChanged(EntityEntry entry, string column)
    {
        var property = entry.Property(column);
        if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
        {
            throw new InvalidOperationException($"{column} cannot change after insertion.");
        }
    }
}
