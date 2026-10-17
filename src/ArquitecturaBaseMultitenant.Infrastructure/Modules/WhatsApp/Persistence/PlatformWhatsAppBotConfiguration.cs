using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El bot del número de Plataforma: una sola fila, global, con su evento de seguridad.</summary>
internal sealed class PlatformWhatsAppBotConfiguration : IEntityTypeConfiguration<PlatformWhatsAppBot>
{
    public void Configure(EntityTypeBuilder<PlatformWhatsAppBot> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformWhatsAppBots", Schemas.Platform);
        builder.HasKey(value => value.Id);
        WhatsAppBotMapping.Configure(builder);
    }
}
