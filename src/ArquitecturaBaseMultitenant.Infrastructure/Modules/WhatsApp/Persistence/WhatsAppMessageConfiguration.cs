using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
internal sealed class WhatsAppMessageConfiguration : IEntityTypeConfiguration<WhatsAppMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessage> builder)
    {
        builder.ToTable("WhatsAppMessages", Schemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.PhoneNumberId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Wamid).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(value => value.EventType).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Direction).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Kind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.ReplyId).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.ContentType).HasMaxLength(TextLimits.ShortName);
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(value => value.ChannelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => value.ChannelId);
        builder.Property(value => value.OriginalUserId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(value => value.OriginalMethodId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasIndex(value => value.OriginalUserId);
        builder.HasIndex(value => new { value.Direction, value.NextAttemptAtUtc, value.LeaseUntilUtc, value.ReceivedAtUtc });
        builder.HasIndex(value => new { value.PhoneNumberId, value.Wamid, value.EventType }).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.MessageEvent);
        builder.HasIndex(value => new { value.ProcessedAtUtc, value.LeaseUntilUtc, value.ReceivedAtUtc });
        builder.HasIndex(value => value.ReceivedAtUtc);
        builder.HasOne<WhatsAppContact>().WithMany().HasForeignKey(value => value.ContactId).OnDelete(DeleteBehavior.SetNull);
    }
}
