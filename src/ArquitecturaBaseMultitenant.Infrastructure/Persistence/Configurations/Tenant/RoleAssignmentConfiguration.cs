using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

/// <summary>Mapea asignaciones privadas y exige que el rol y la membresía pertenezcan al mismo tenant.</summary>
internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments", Schemas.Tenant);
        builder.HasKey(assignment => new { assignment.TenantId, assignment.Id });
        builder.HasIndex(assignment => new { assignment.TenantId, assignment.MemberId, assignment.RoleId })
            .IsUnique().HasFilter("\"CompanyId\" IS NULL");
        builder.HasIndex(assignment => new
            { assignment.TenantId, assignment.MemberId, assignment.RoleId, assignment.CompanyId })
            .IsUnique().HasFilter("\"CompanyId\" IS NOT NULL");
        builder.HasIndex(assignment => new
            { assignment.TenantId, assignment.RoleId, assignment.MemberId, assignment.Id });
        builder.HasOne<Member>().WithMany()
            .HasForeignKey(assignment => new { assignment.TenantId, assignment.MemberId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany()
            .HasForeignKey(assignment => new { assignment.TenantId, assignment.RoleId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
