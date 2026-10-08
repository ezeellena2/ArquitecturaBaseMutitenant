using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;
internal sealed class LoginLinkConfiguration : IEntityTypeConfiguration<LoginLink>
{
    public void Configure(EntityTypeBuilder<LoginLink> builder)
    {
        builder.ToTable("LoginLinks", Schemas.Identity);
        builder.HasKey(link => link.Id);
        builder.Property(link => link.TokenHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.HasIndex(link => link.TokenHash).IsUnique();
        builder.HasIndex(link => new { link.UserId, link.CreatedAtUtc });
        // MethodId is an immutable proof reference; deleting a method must not delete the issue history.
    }
}
