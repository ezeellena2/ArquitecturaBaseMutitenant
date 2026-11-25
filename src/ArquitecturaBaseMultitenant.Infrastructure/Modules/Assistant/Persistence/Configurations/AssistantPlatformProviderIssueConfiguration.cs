using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantPlatformProviderIssueConfiguration : IEntityTypeConfiguration<AssistantPlatformProviderIssue>
{
    public void Configure(EntityTypeBuilder<AssistantPlatformProviderIssue> builder)
    {
        builder.ToTable("AssistantPlatformProviderIssues", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Code).HasMaxLength(TextLimits.ShortName);
    }
}
