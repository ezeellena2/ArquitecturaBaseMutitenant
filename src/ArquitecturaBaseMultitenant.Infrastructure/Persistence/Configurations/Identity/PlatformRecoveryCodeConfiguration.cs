using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Los códigos de recuperación (E9): sólo el hash con clave, único por cuenta.</summary>
internal sealed class PlatformRecoveryCodeConfiguration : IEntityTypeConfiguration<PlatformRecoveryCode>
{
    public void Configure(EntityTypeBuilder<PlatformRecoveryCode> builder)
    {
        builder.ToTable("PlatformRecoveryCodes", Schemas.Identity);
        builder.HasKey(code => code.Id);
        builder.Property(code => code.CodeHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.HasIndex(code => new { code.UserId, code.CodeHash }).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(code => code.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
