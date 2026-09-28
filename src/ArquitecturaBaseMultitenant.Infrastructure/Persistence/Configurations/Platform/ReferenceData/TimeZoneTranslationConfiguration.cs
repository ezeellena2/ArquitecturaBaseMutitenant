using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform.ReferenceData;

internal sealed class TimeZoneTranslationConfiguration : IEntityTypeConfiguration<TimeZoneTranslation>
{
    public void Configure(EntityTypeBuilder<TimeZoneTranslation> builder)
    {
        builder.ToTable("TimeZoneTranslations", "platform");
        builder.HasKey(translation => new { translation.TimeZoneId, translation.Culture });
        builder.Property(translation => translation.TimeZoneId).IsRequired();
        builder.Property(translation => translation.Culture).IsRequired();
        builder.Property(translation => translation.City).IsRequired();
        builder.HasOne<ReferenceTimeZone>().WithMany().HasForeignKey(translation => translation.TimeZoneId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(translation => translation.Culture)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
