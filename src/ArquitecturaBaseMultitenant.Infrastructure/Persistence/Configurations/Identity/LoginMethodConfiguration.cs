using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Mapea los métodos de ingreso globales de una cuenta. Un método verificado es único en todo el sistema por tipo y
/// valor; uno pendiente, solo dentro de su cuenta, así un correo de otra cuenta queda pendiente como uno libre (ADR
/// 0041, D4b). Solo puede haber un método principal por usuario.
/// </summary>
internal sealed class LoginMethodConfiguration : IEntityTypeConfiguration<LoginMethod>
{
    public void Configure(EntityTypeBuilder<LoginMethod> builder)
    {
        builder.ToTable("LoginMethods", Schemas.Identity);
        builder.HasKey(method => method.Id);
        builder.Property(method => method.Type).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(method => method.Value).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(method => method.InvitationOriginTenantId);
        builder.HasIndex(method => new { method.Type, method.Value }).IsUnique().HasDatabaseName(UniqueKeys.VerifiedLoginMethod)
            .HasFilter("\"VerifiedAtUtc\" IS NOT NULL");
        builder.HasIndex(method => new { method.UserId, method.Type, method.Value }).IsUnique();
        builder.HasIndex(method => method.UserId);
        builder.HasIndex(method => method.UserId, "IX_LoginMethods_UserId_Primary")
            .IsUnique().HasFilter("\"IsPrimary\" = TRUE");
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(method => method.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
