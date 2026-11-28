using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// Lo común de las conversaciones de organización y de Plataforma. El contenido (la vista previa y el texto) va cifrado con el
/// propósito de su esquema (ADR 0065), que da cada configuración.
/// </summary>
internal static class WhatsAppChatMapping
{
    internal static void Configure<T>(EntityTypeBuilder<T> builder, WhatsAppChatContentConverter content) where T : WhatsAppChatBase
    {
        builder.Property(value => value.WaId).HasMaxLength(PhoneNumber.MaxLength).IsRequired();
        builder.Property(value => value.LastMessagePreview).HasConversion(content).HasColumnType("bytea");
        builder.Ignore(value => value.WindowOpenUntilUtc);
        builder.Ignore(value => value.IsTaken);
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(value => value.ChannelId).OnDelete(DeleteBehavior.Restrict);
    }

    internal static void ConfigureMessage<T>(EntityTypeBuilder<T> builder, WhatsAppChatContentConverter content) where T : WhatsAppChatMessageBase
    {
        builder.Property(value => value.Kind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Direction).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.MediaKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.AuthorName).HasMaxLength(TextLimits.PersonDisplayName);
        builder.Property(value => value.Text).HasConversion(content).HasColumnType("bytea");
        builder.Property(value => value.FailureReason).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Wamid).HasMaxLength(TextLimits.Description);
        builder.Property(value => value.ReviewedByName).HasMaxLength(TextLimits.PersonDisplayName);
        builder.Property(value => value.SentFromPhone);
    }
}
