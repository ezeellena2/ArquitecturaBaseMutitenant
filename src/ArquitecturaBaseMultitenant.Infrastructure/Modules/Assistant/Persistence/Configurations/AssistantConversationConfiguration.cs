using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("AssistantConversations", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(value => value.Title);
        builder.Ignore(value => value.LastActivityAtUtc);
        builder.Property(value => value.ManualTitle).HasColumnName("Title").HasMaxLength(TextLimits.AssistantTitle);
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Navigation(value => value.Activity).IsRequired().AutoInclude();
    }
}
