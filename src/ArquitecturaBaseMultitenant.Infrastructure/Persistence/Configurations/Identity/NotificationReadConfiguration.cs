using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Qué avisos de la campana abrió cada cuenta (base de la 12): global, por cuenta y con su clave única.</summary>
internal sealed class NotificationReadConfiguration : IEntityTypeConfiguration<NotificationRead>
{
    public void Configure(EntityTypeBuilder<NotificationRead> builder)
    {
        builder.ToTable("NotificationReads", Schemas.Identity);
        builder.HasKey(read => read.Id);
        builder.Property(read => read.NotificationId).HasMaxLength(TextLimits.NotificationId).IsRequired();
        builder.HasIndex(read => new { read.UserId, read.NotificationId }).IsUnique()
            .HasDatabaseName(UniqueKeys.NotificationRead);
        builder.HasIndex(read => read.ReadAtUtc);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(read => read.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
