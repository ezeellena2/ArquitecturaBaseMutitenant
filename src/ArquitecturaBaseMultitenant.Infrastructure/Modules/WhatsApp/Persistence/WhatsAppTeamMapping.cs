using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Lo común de los equipos de organización y de Plataforma.</summary>
internal static class WhatsAppTeamMapping
{
    internal static void Configure<T>(EntityTypeBuilder<T> builder) where T : WhatsAppTeamBase
    {
        builder.Property(value => value.Name).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.NormalizedName).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.MemberIds).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field).HasColumnType("uuid[]").IsRequired();
        builder.HasIndex(value => value.MemberIds).HasMethod("gin");
    }
}
