using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Registro global de números: un identificador de Meta vigente en un solo lugar; los desconectados quedan para leer.</summary>
internal sealed class WhatsAppChannelConfiguration : IEntityTypeConfiguration<WhatsAppChannel>
{
    public void Configure(EntityTypeBuilder<WhatsAppChannel> builder)
    {
        builder.ToTable("WhatsAppChannels", Schemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.PhoneNumberId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.WabaId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.DisplayNumber).HasMaxLength(PhoneNumber.MaxLength);
        builder.Property(value => value.VerifiedName).HasMaxLength(TextLimits.OrganizationName);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Source).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.UsageTimeZoneId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.EncryptedToken).IsRequired();
        builder.Ignore(value => value.IsDisconnected);
        builder.HasIndex(value => value.PhoneNumberId).IsUnique()
            .HasFilter("\"Status\" <> 'Disconnected'").HasDatabaseName(WhatsAppUniqueKeys.ActiveChannelNumber);
        builder.HasIndex(value => new { value.OrganizationId, value.DisplayNumber, value.Id });
        builder.HasIndex(value => value.WabaId);
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
