using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>El vínculo durable de cada operación del segundo factor (E9) con su clave de idempotencia.</summary>
internal sealed class PlatformFactorOperationConfiguration : IEntityTypeConfiguration<PlatformFactorOperation>
{
    public void Configure(EntityTypeBuilder<PlatformFactorOperation> builder)
    {
        builder.ToTable("PlatformFactorOperations", Schemas.Identity);
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Kind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(operation => operation.BodyHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(operation => operation.Route).HasMaxLength(TextLimits.WebAddress).IsRequired();
        builder.Property(operation => operation.ReturnUrl).HasMaxLength(TextLimits.LongText);
        builder.HasIndex(operation => operation.IdempotencyKey).IsUnique();
        builder.HasIndex(operation => new { operation.UserId, operation.CreatedAtUtc });
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(operation => operation.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
