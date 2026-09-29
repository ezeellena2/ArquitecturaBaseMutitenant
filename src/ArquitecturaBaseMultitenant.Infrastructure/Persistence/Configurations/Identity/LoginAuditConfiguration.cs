using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

internal sealed class LoginAuditConfiguration : IEntityTypeConfiguration<LoginAudit>
{
    public void Configure(EntityTypeBuilder<LoginAudit> builder)
    {
        builder.ToTable("LoginAudits", Schemas.Identity);
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Method).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(audit => audit.FailureCode).HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(audit => new { audit.UserId, audit.OccurredAtUtc });
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(audit => audit.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
