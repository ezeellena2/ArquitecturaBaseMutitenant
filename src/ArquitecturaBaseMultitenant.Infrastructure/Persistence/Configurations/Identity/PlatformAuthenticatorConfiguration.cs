using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>El autenticador de un operador (E9): uno por cuenta, con la clave protegida.</summary>
internal sealed class PlatformAuthenticatorConfiguration : IEntityTypeConfiguration<PlatformAuthenticator>
{
    public void Configure(EntityTypeBuilder<PlatformAuthenticator> builder)
    {
        builder.ToTable("PlatformAuthenticators", Schemas.Identity);
        builder.HasKey(authenticator => authenticator.Id);
        builder.Property(authenticator => authenticator.ProtectedSecret).IsRequired();
        builder.HasIndex(authenticator => authenticator.UserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(authenticator => authenticator.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
