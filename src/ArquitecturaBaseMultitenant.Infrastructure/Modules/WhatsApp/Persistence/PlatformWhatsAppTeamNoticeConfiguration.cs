using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El aviso ya enviado de un equipo de operadores de Plataforma.</summary>
internal sealed class PlatformWhatsAppTeamNoticeConfiguration : IEntityTypeConfiguration<PlatformWhatsAppTeamNotice>
{
    public void Configure(EntityTypeBuilder<PlatformWhatsAppTeamNotice> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformWhatsAppTeamNotices", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.TeamId).IsUnique();
        builder.HasOne<PlatformWhatsAppTeam>().WithMany().HasForeignKey(value => value.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}
