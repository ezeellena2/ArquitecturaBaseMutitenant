using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

/// <summary>0064 §11: <c>platform.AiKeyRingControls</c>, global (el anillo es uno de la instalación) y sin borrado.</summary>
internal sealed class AiKeyRingControlConfiguration : IEntityTypeConfiguration<AiKeyRingControl>
{
    public void Configure(EntityTypeBuilder<AiKeyRingControl> builder)
    {
        builder.ToTable("AiKeyRingControls", Schemas.Platform);
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("KeyId").ValueGeneratedNever();
        builder.Ignore(row => row.KeyId);
        builder.Property(row => row.Control).IsRequired();
    }
}
