using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Mapea la cuenta global de ASP.NET Identity y sus datos de perfil y baja. El correo y teléfono son copias del método principal, no claves de búsqueda.</summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers", Schemas.Identity);
        builder.HasKey(user => user.Id);
        builder.Property(user => user.DisplayName).HasMaxLength(TextLimits.PersonName);
        builder.Property(user => user.Culture).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(user => user.TimeZoneId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(user => user.DeletionReason).HasMaxLength(TextLimits.Description);
        builder.HasIndex(user => user.DeletionScheduledForUtc);
    }
}
