using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

internal sealed class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings", Schemas.Platform);
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.ConsumerSignup).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(settings => settings.BusinessSignup).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
    }
}
