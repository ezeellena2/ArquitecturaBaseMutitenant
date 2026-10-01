using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

/// <summary>Invitación privada con vínculo al miembro de la misma organización y unicidad del destino pendiente.</summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations", Schemas.Tenant);
        builder.HasKey(invitation => new { invitation.TenantId, invitation.Id });
        builder.Property(invitation => invitation.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(invitation => invitation.Channel).HasMaxLength(TextLimits.ShortName);
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(TextLimits.ShortName);
        builder.Property(invitation => invitation.BootstrapNonceHash).HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Destination }).IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.TokenHash }).IsUnique();
        builder.HasOne<Member>().WithMany()
            .HasForeignKey(invitation => new { invitation.TenantId, invitation.MemberId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(invitation => invitation.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(invitation => invitation.InviterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
