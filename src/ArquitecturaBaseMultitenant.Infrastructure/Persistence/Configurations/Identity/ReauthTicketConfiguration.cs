using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Mapea los comprobantes breves de reautenticación por cuenta y acción. El hash del token es único y su vencimiento permite descartar el uso tardío.</summary>
internal sealed class ReauthTicketConfiguration : IEntityTypeConfiguration<ReauthTicket>
{
    public void Configure(EntityTypeBuilder<ReauthTicket> builder)
    {
        builder.ToTable("ReauthTickets", Schemas.Identity);
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Action).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(ticket => ticket.TokenHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(ticket => ticket.ReturnUrl).HasMaxLength(TextLimits.LongText);
        builder.HasIndex(ticket => ticket.TokenHash).IsUnique();
        builder.HasIndex(ticket => new { ticket.UserId, ticket.ExpiresAtUtc });
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(ticket => ticket.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

