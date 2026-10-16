using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantOrganizationPolicyConfiguration : IEntityTypeConfiguration<AssistantOrganizationPolicy>
{
    public void Configure(EntityTypeBuilder<AssistantOrganizationPolicy> builder)
    {
        builder.ToTable("AssistantOrganizationPolicies", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.OrganizationId).IsUnique();
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}
