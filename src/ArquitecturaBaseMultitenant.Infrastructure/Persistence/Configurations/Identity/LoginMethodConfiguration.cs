using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

internal sealed class LoginMethodConfiguration : IEntityTypeConfiguration<LoginMethod>
{
    public void Configure(EntityTypeBuilder<LoginMethod> builder)
    {
        builder.ToTable("LoginMethods", Schemas.Identity);
        builder.HasKey(method => method.Id);
        builder.Property(method => method.Type).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(method => method.Value).HasMaxLength(TextLimits.Description).IsRequired();
        builder.HasIndex(method => new { method.Type, method.Value }).IsUnique();
        builder.HasIndex(method => method.UserId);
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(method => method.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
