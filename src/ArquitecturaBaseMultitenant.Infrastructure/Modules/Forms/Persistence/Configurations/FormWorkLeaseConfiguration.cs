using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Configurations;

/// <summary>
/// La barrera de las operaciones de archivo (C11): técnica, sin auditoría, privada y con RLS. Una por persona en la organización:
/// el índice único <c>(TenantId, UserId)</c> es el blanco del <c>INSERT … ON CONFLICT</c> que pisa solo una vencida.
/// </summary>
internal sealed class FormWorkLeaseConfiguration : IEntityTypeConfiguration<FormWorkLease>
{
    public void Configure(EntityTypeBuilder<FormWorkLease> builder)
    {
        builder.ToTable("FormWorkLeases", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Kind).HasConversion<string>().HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.HasIndex(value => new { value.TenantId, value.UserId }).IsUnique();
    }
}
