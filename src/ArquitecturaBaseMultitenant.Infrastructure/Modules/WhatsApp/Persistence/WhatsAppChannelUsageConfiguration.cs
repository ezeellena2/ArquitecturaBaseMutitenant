using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppChannelUsageConfiguration : IEntityTypeConfiguration<WhatsAppChannelUsage>
{
    public void Configure(EntityTypeBuilder<WhatsAppChannelUsage> builder)
    {
        builder.ToTable("WhatsAppChannelUsages", Schemas.Identity);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => new { value.ChannelId, value.Month }).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.ChannelUsageMonth);
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(value => value.ChannelId).OnDelete(DeleteBehavior.Cascade);
    }
}
