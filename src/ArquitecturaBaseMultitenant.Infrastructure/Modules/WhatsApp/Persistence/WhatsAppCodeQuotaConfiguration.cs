using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
internal sealed class WhatsAppCodeQuotaConfiguration : IEntityTypeConfiguration<WhatsAppCodeQuota>
{
    public void Configure(EntityTypeBuilder<WhatsAppCodeQuota> builder)
    {
        builder.ToTable("WhatsAppCodeQuotas", Schemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.PhoneNumberId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.HasIndex(value => new { value.PhoneNumberId, value.DayUtc }).IsUnique();
    }
}
