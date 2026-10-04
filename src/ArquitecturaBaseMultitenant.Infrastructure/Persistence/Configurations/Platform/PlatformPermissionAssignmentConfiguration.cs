using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Una asignación global de permisos por cuenta de operador, sin RLS ni filtro de organización.</summary>
internal sealed class PlatformPermissionAssignmentConfiguration : IEntityTypeConfiguration<PlatformPermissionAssignment>
{
    public void Configure(EntityTypeBuilder<PlatformPermissionAssignment> builder)
    {
        builder.ToTable("PlatformPermissionAssignments", Schemas.Platform);
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Permissions).HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field).HasColumnType("text[]").IsRequired();
        builder.HasIndex(assignment => assignment.UserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(assignment => assignment.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
