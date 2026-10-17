using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Los equipos de la organización: un nombre por organización y los integrantes como lista de miembros.</summary>
internal sealed class WhatsAppTeamConfiguration : IEntityTypeConfiguration<WhatsAppTeam>
{
    public void Configure(EntityTypeBuilder<WhatsAppTeam> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("WhatsAppTeams", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        WhatsAppTeamMapping.Configure(builder);
        builder.Property(value => value.Inbox).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => new { value.TenantId, value.NormalizedName }).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.TeamName);
        builder.HasIndex(value => new { value.TenantId, value.Name, value.Id });
    }
}
