using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// Lo común de los bots de organización y de Plataforma. El horario (hasta 7 días) y el menú (hasta 10 opciones) van como
/// jsonb del bot: se leen y se reemplazan enteros, y la auditoría ve el cambio como una propiedad.
/// </summary>
internal static class WhatsAppBotMapping
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    internal static void Configure<T>(EntityTypeBuilder<T> builder) where T : WhatsAppBotBase
    {
        builder.Property(value => value.Welcome).HasMaxLength(WhatsAppTextLimits.BotText).IsRequired();
        builder.Property(value => value.OutOfHours).HasMaxLength(WhatsAppTextLimits.BotText).IsRequired();
        builder.Property(value => value.TimeZoneId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property<List<WhatsAppBotHours>>("_hours").HasColumnName("Hours").HasColumnType("jsonb").IsRequired()
            .HasConversion(value => JsonSerializer.Serialize(value, Json), value => JsonSerializer.Deserialize<List<WhatsAppBotHours>>(value, Json) ?? new(),
                Comparer<WhatsAppBotHours>());
        builder.Property<List<WhatsAppBotOption>>("_options").HasColumnName("Options").HasColumnType("jsonb").IsRequired()
            .HasConversion(value => JsonSerializer.Serialize(value, Json), value => JsonSerializer.Deserialize<List<WhatsAppBotOption>>(value, Json) ?? new(),
                Comparer<WhatsAppBotOption>());
        builder.Ignore(value => value.Hours);
        builder.Ignore(value => value.Options);
    }

    private static ValueComparer<List<TItem>> Comparer<TItem>() => new(
        (left, right) => left!.SequenceEqual(right!),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        value => value.ToList());
}
