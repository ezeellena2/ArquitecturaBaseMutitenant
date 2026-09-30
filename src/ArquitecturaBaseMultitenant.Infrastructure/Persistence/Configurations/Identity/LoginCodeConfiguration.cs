using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

internal sealed class LoginCodeConfiguration : IEntityTypeConfiguration<LoginCode>
{
    public void Configure(EntityTypeBuilder<LoginCode> builder)
    {
        builder.ToTable("LoginCodes", Schemas.Identity);
        builder.HasKey(code => code.Id);
        builder.Property(code => code.Destination).HasMaxLength(Email.MaxLength).IsRequired();
        builder.Property(code => code.Channel).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(code => code.CodeHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(code => code.Purpose).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(code => code.ReauthAction).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(code => new { code.Destination, code.Channel, code.Purpose, code.ExpiresAtUtc });
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(code => code.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
