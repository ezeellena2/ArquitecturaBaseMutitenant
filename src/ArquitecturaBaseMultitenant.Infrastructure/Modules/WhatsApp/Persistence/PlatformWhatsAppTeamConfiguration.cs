using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Los equipos de operadores de Plataforma; los integrantes son cuentas.</summary>
internal sealed class PlatformWhatsAppTeamConfiguration : IEntityTypeConfiguration<PlatformWhatsAppTeam>
{
    public void Configure(EntityTypeBuilder<PlatformWhatsAppTeam> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformWhatsAppTeams", Schemas.Platform);
        builder.HasKey(value => value.Id);
        WhatsAppTeamMapping.Configure(builder);
        builder.HasIndex(value => value.NormalizedName).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.PlatformTeamName);
        builder.HasIndex(value => new { value.Name, value.Id });
    }
}
