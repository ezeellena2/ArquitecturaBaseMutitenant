using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantOrganizationProviderIssueConfiguration : IEntityTypeConfiguration<AssistantOrganizationProviderIssue>
{
    public void Configure(EntityTypeBuilder<AssistantOrganizationProviderIssue> builder)
    {
        builder.ToTable("AssistantOrganizationProviderIssues", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => value.TenantId).IsUnique();
        builder.Property(value => value.Code).HasMaxLength(TextLimits.ShortName);
    }
}
