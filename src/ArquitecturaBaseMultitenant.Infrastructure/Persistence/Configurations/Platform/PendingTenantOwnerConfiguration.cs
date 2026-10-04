using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Una fila por invitación pendiente del Dueño de una organización creada desde Plataforma.</summary>
internal sealed class PendingTenantOwnerConfiguration : IEntityTypeConfiguration<PendingTenantOwner>
{
    public void Configure(EntityTypeBuilder<PendingTenantOwner> builder)
    {
        builder.ToTable("PendingTenantOwners", Schemas.Platform);
        builder.HasKey(owner => owner.InvitationId);
        builder.Property(owner => owner.Address).HasMaxLength(Email.MaxLength).IsRequired();
        builder.HasIndex(owner => owner.TenantId);
        builder.HasOne<TenantEntity>().WithMany().HasForeignKey(owner => owner.TenantId).OnDelete(DeleteBehavior.Cascade);
        foreach (var property in builder.Metadata.GetProperties())
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
