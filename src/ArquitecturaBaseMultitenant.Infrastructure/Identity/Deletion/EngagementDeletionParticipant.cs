using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

/// <summary>Reemplaza la copia de la persona o fija su retención legal dentro del paso Personal de la baja.</summary>
internal sealed class EngagementDeletionParticipant(ApplicationDbContext database, ITenantContext tenant,
    EngagementCopyDeletion copies) : AccountDeletionParticipant
{
    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantKind != TenantKind.Personal || context.TenantId is not { } consumerTenantId) return;
        database.RequireTransaction();
        if (tenant.RequiredTenantId != consumerTenantId)
            throw new InvalidOperationException("Shared consumer deletion needs its personal tenant scope.");
        foreach (var entityType in copies.SharedTypes())
        {
            var policy = copies.RetentionFor(entityType.ClrType);
            var entityTypeKey = EngagementCopyDeletion.Key(entityType);
            foreach (var row in await copies.ReadConsumerRowsAsync(entityType.ClrType, consumerTenantId, ct))
            {
                var shared = (IConsumerBusinessShared)row;
                // El plazo fijado también rige si una versión posterior cambia o retira la política del módulo.
                if (database.ConsumerDataRetentions.Local.Any(retention => retention.EntityTypeKey == entityTypeKey
                    && retention.EntityId == row.Id && retention.ConsumerTenantId == consumerTenantId
                    && retention.BusinessTenantId == shared.BusinessTenantId)
                    || await database.ConsumerDataRetentions.AnyAsync(retention => retention.EntityTypeKey == entityTypeKey
                        && retention.EntityId == row.Id && retention.ConsumerTenantId == consumerTenantId
                        && retention.BusinessTenantId == shared.BusinessTenantId, ct)) continue;
                if (policy is null)
                {
                    shared.ReplaceConsumerCopy();
                    continue;
                }

                database.ConsumerDataRetentions.Add(ConsumerDataRetention.Schedule(entityTypeKey, row.Id,
                    consumerTenantId, shared.BusinessTenantId, policy.Reason, context.NowUtc.Add(policy.RetentionPeriod)));
            }
        }
    }
}
