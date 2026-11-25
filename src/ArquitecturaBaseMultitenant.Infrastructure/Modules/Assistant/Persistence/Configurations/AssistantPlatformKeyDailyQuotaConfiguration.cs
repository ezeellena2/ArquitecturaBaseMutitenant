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
        // 271: un cupo por persona y organización, y uno fuera de una (nula), sin que dos nulas cuenten como distintas.
        builder.HasIndex(value => new { value.UserId, value.OrganizationId, value.DayUtc }).IsUnique().AreNullsDistinct(false);
    }
}
