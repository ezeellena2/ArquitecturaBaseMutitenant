using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>
/// Pedidos globales de «Recuperar mi cuenta», sin RLS ni organización. Los valores quedan en claro, como los de
/// <c>identity.LoginMethods</c>, porque la bandeja busca por ellos y aprobar crea el método nuevo.
/// </summary>
internal sealed class AccountRecoveryRequestConfiguration : IEntityTypeConfiguration<AccountRecoveryRequest>
{
    public void Configure(EntityTypeBuilder<AccountRecoveryRequest> builder)
    {
        builder.ToTable("AccountRecoveryRequests", Schemas.Platform);
        builder.HasKey(request => request.Id);
        builder.Property(request => request.PreviousMethodType).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(request => request.PreviousMethodValue).HasMaxLength(Email.MaxLength).IsRequired();
        builder.Property(request => request.NewMethodType).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(request => request.NewMethodValue).HasMaxLength(Email.MaxLength).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(request => request.DecisionReason).HasMaxLength(TextLimits.Description);
        builder.HasIndex(request => request.LoginCodeId).IsUnique();
        builder.HasIndex(request => new { request.Status, request.CreatedAtUtc, request.Id });
        builder.HasIndex(request => request.UserId);
        builder.HasIndex(request => new { request.CreatedAtUtc, request.Id });
    }
}
