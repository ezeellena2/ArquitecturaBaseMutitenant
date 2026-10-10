using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantOwnKeyDailyQuotaConfiguration : IEntityTypeConfiguration<AssistantOwnKeyDailyQuota>
{
    public void Configure(EntityTypeBuilder<AssistantOwnKeyDailyQuota> builder)
    {
        builder.ToTable("AssistantOwnKeyDailyQuotas", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.TenantId, value.UserId, value.DayUtc }).IsUnique();
    }
}
