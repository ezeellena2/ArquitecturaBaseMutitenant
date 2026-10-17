using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El bot de cada número de la organización: uno por número, privado y auditado.</summary>
internal sealed class WhatsAppBotConfiguration : IEntityTypeConfiguration<WhatsAppBot>
{
    public void Configure(EntityTypeBuilder<WhatsAppBot> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("WhatsAppBots", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        WhatsAppBotMapping.Configure(builder);
        builder.HasIndex(value => new { value.TenantId, value.ChannelId }).IsUnique();
        builder.HasOne<WhatsAppChannel>().WithMany().HasForeignKey(value => value.ChannelId).OnDelete(DeleteBehavior.Restrict);
        // Borrar un equipo saca antes sus opciones y el de empleados (LeaveTeam): la clave no deja referencias sueltas.
        builder.HasIndex(value => new { value.TenantId, value.EmployeesTeamId });
        builder.HasOne<WhatsAppTeam>().WithMany().HasForeignKey(value => new { value.TenantId, value.EmployeesTeamId }).OnDelete(DeleteBehavior.Restrict);
    }
}
