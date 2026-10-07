using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;
/// <summary>Mapea las pruebas de cancelación de baja globales y su hash único.</summary>
internal sealed class AccountDeletionCancelTicketConfiguration : IEntityTypeConfiguration<AccountDeletionCancelTicket>
{
    public void Configure(EntityTypeBuilder<AccountDeletionCancelTicket> builder)
    {
        builder.ToTable("AccountDeletionCancelTickets", Schemas.Identity);
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.TokenHash).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(ticket => ticket.ReturnUrl).HasMaxLength(TextLimits.LongText).IsRequired();
        builder.HasIndex(ticket => ticket.TokenHash).IsUnique();
        builder.HasIndex(ticket => new { ticket.UserId, ticket.ExpiresAtUtc });
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(ticket => ticket.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
