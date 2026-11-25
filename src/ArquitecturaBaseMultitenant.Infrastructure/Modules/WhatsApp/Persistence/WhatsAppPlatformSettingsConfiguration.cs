using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// La configuración de WhatsApp de la plataforma (270 y 272): una sola fila global, con su clave y sus cotas también en la
/// base. Los países se guardan enteros, como JSONB, y se comparan por su contenido (copiado de ArquitecturaBase, RC3-A).
/// </summary>
internal sealed class WhatsAppPlatformSettingsConfiguration : IEntityTypeConfiguration<WhatsAppPlatformSettings>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<WhatsAppPlatformSettings> builder)
    {
        builder.ToTable("WhatsAppPlatformSettings", Schemas.Platform, table =>
        {
            table.HasCheckConstraint("CK_WhatsAppPlatformSettings_Singleton",
                $"\"Id\" = '{WhatsAppPlatformSettings.SingletonIdValue}'");
            table.HasCheckConstraint("CK_WhatsAppPlatformSettings_ChatRetentionDays",
                $"\"ChatRetentionDays\" BETWEEN {WhatsAppSettingsLimits.MinChatRetentionDays} AND {WhatsAppSettingsLimits.MaxChatRetentionDays}");
            table.HasCheckConstraint("CK_WhatsAppPlatformSettings_MaxOrganizationChatRetentionDays",
                $"\"MaxOrganizationChatRetentionDays\" BETWEEN {WhatsAppSettingsLimits.MinChatRetentionDays} AND {WhatsAppSettingsLimits.MaxChatRetentionDays}");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.AllowedCountries).HasColumnType("jsonb").HasConversion(
            value => JsonSerializer.Serialize(value, JsonOptions),
            value => JsonSerializer.Deserialize<IReadOnlyList<string>>(value, JsonOptions)!,
            new ValueComparer<IReadOnlyList<string>>((left, right) => left!.SequenceEqual(right!),
                value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(StringComparison.Ordinal),
                value => value.ToArray()));
    }
}
