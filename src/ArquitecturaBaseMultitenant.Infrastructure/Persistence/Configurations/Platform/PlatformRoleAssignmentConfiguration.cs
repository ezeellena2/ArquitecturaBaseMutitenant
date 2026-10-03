using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Una asignación global por cuenta de operador, sin RLS ni filtro de organización.</summary>
internal sealed class PlatformRoleAssignmentConfiguration : IEntityTypeConfiguration<PlatformRoleAssignment>
{
    public void Configure(EntityTypeBuilder<PlatformRoleAssignment> builder)
    {
        builder.ToTable("PlatformRoleAssignments", Schemas.Platform);
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Role).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(assignment => assignment.UserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(assignment => assignment.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
