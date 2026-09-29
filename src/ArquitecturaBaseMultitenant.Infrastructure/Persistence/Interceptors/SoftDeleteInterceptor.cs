using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;

/// <summary>Convierte Delete en una marca de borrado lógico.</summary>
internal sealed class SoftDeleteInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        SoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        SoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var deleted = context.ChangeTracker.Entries<ISoftDeletable>()
            .Where(entry => entry.State == EntityState.Deleted)
            .ToArray();
        if (deleted.Length == 0)
        {
            return;
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in deleted)
        {
            entry.State = EntityState.Unchanged;
            var isDeleted = entry.Property(nameof(ISoftDeletable.IsDeleted));
            isDeleted.CurrentValue = true;
            isDeleted.IsModified = true;
            var deletedAtUtc = entry.Property(nameof(ISoftDeletable.DeletedAtUtc));
            deletedAtUtc.CurrentValue = nowUtc;
            deletedAtUtc.IsModified = true;
            var deletedBy = entry.Property(nameof(ISoftDeletable.DeletedBy));
            deletedBy.CurrentValue = currentUser.UserId;
            deletedBy.IsModified = true;
        }
    }
}
