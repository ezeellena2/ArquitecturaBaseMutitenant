using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Mapea los ajustes globales de altas y límites de la plataforma. Sus modos se guardan como texto para que el estado sea legible en la base.</summary>
internal sealed class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings", Schemas.Platform);
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.ConsumerSignup).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(settings => settings.BusinessSignup).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(settings => settings.SalesEmail).HasMaxLength(Email.MaxLength);
        builder.Property(settings => settings.SupportEmail).HasMaxLength(Email.MaxLength);
        // 272 y 273: la versión anterior no conoce estas columnas y su alta toma el valor por defecto de la base (0060).
        builder.Property(settings => settings.LoginAuditRetentionMonths)
            .HasDefaultValue(PlatformSettingsLimits.DefaultLoginAuditRetentionMonths);
        builder.Property(settings => settings.LoginCodeRetentionDays)
            .HasDefaultValue(PlatformSettingsLimits.DefaultLoginCodeRetentionDays);
        builder.Property(settings => settings.ClosedInvitationRetentionDays)
            .HasDefaultValue(PlatformSettingsLimits.DefaultClosedInvitationRetentionDays);
        builder.Property(settings => settings.AppName).HasMaxLength(PlatformSettingsLimits.NameMaxLength);
        builder.Property(settings => settings.SenderName).HasMaxLength(PlatformSettingsLimits.NameMaxLength);
        builder.Property(settings => settings.EmailLogoUrl).HasMaxLength(PlatformSettingsLimits.LogoUrlMaxLength);
    }
}
