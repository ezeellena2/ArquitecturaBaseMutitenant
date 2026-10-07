using ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

internal sealed class LoginInvitationConfiguration : IEntityTypeConfiguration<LoginInvitation>
{
    public void Configure(EntityTypeBuilder<LoginInvitation> builder)
    {
        builder.ToTable("LoginInvitations", Schemas.Identity);
        builder.HasKey(row => new { row.TenantId, row.InvitationId });
        builder.HasIndex(row => new { row.Destination, row.ExpiresAtUtc });
        builder.HasOne<Domain.Tenancy.Tenant>().WithMany().HasForeignKey(row => row.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
