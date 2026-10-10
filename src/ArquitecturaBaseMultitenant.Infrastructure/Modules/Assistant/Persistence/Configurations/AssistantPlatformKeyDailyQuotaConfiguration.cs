using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantPlatformKeyDailyQuotaConfiguration : IEntityTypeConfiguration<AssistantPlatformKeyDailyQuota>
{
    public void Configure(EntityTypeBuilder<AssistantPlatformKeyDailyQuota> builder)
    {
        builder.ToTable("AssistantPlatformKeyDailyQuotas", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => new { value.UserId, value.DayUtc }).IsUnique();
    }
}
