using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>La sesión de Plataforma (E9): el hash del identificador de la cookie y la prueba reciente.</summary>
internal sealed class PlatformSessionConfiguration : IEntityTypeConfiguration<PlatformSession>
{
    public void Configure(EntityTypeBuilder<PlatformSession> builder)
    {
        builder.ToTable("PlatformSessions", Schemas.Identity);
        builder.HasKey(session => session.Id);
        builder.Property(session => session.SessionIdHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(session => session.SecurityStamp).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(session => session.Method).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(session => session.SessionIdHash).IsUnique();
        builder.HasIndex(session => new { session.UserId, session.StartedAtUtc });
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
