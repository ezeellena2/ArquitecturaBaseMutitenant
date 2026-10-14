using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El aviso ya enviado de un equipo de la organización: uno por equipo, se va con el equipo.</summary>
internal sealed class WhatsAppTeamNoticeConfiguration : IEntityTypeConfiguration<WhatsAppTeamNotice>
{
    public void Configure(EntityTypeBuilder<WhatsAppTeamNotice> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("WhatsAppTeamNotices", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.TenantId, value.TeamId }).IsUnique();
        builder.HasOne<WhatsAppTeam>().WithMany().HasForeignKey(value => new { value.TenantId, value.TeamId }).OnDelete(DeleteBehavior.Cascade);
    }
}
