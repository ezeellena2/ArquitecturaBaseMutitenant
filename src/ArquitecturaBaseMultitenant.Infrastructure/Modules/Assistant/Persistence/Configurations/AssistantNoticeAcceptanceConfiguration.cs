using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantNoticeAcceptanceConfiguration : IEntityTypeConfiguration<AssistantNoticeAcceptance>
{
    public void Configure(EntityTypeBuilder<AssistantNoticeAcceptance> builder)
    {
        builder.ToTable("AssistantNoticeAcceptances", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.NoticeVersion).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => new { value.OwnerUserId, value.Access, value.NoticeVersion }).IsUnique();
    }
}
