using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppCodeQuotaIpConfiguration : IEntityTypeConfiguration<WhatsAppCodeQuotaIp>
{
    public void Configure(EntityTypeBuilder<WhatsAppCodeQuotaIp> builder)
    {
        builder.ToTable("WhatsAppCodeQuotaIps", Schemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.IpKey).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.HasIndex(value => new { value.QuotaId, value.IpKey }).IsUnique();
        builder.HasOne<WhatsAppCodeQuota>().WithMany().HasForeignKey(value => value.QuotaId).OnDelete(DeleteBehavior.Cascade);
    }
}
