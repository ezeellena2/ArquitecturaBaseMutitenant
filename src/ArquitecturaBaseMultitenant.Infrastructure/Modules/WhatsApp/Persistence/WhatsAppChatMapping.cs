using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Lo común de las conversaciones de organización y de Plataforma.</summary>
internal static class WhatsAppChatMapping
{
    internal static void Configure<T>(EntityTypeBuilder<T> builder) where T : WhatsAppChatBase
    {
        builder.Property(value => value.WaId).HasMaxLength(PhoneNumber.MaxLength).IsRequired();
        builder.Property(value => value.LastMessagePreview).HasMaxLength(WhatsAppChatBase.PreviewLength);
        builder.Ignore(value => value.WindowOpenUntilUtc);
        builder.Ignore(value => value.IsTaken);
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(value => value.ChannelId).OnDelete(DeleteBehavior.Restrict);
    }

    internal static void ConfigureMessage<T>(EntityTypeBuilder<T> builder) where T : WhatsAppChatMessageBase
    {
        builder.Property(value => value.Kind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Direction).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.MediaKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.AuthorName).HasMaxLength(TextLimits.PersonDisplayName);
        builder.Property(value => value.Text).HasMaxLength(WhatsAppTextLimits.MessageText);
        builder.Property(value => value.FailureReason).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Wamid).HasMaxLength(TextLimits.Description);
    }
}
