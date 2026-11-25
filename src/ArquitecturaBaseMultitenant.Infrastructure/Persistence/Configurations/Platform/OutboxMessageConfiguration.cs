using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Mapea los avisos pendientes y su payload cifrado en platform. Sus índices permiten reclamar el próximo intento, por prioridad y por destino, y cancelar los pendientes de una cuenta.</summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", Schemas.Platform);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Channel).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(message => message.EncryptedPayload).HasColumnType("text").IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(message => new { message.Status, message.NextAttemptAtUtc, message.Id });
        builder.HasIndex(message => message.UserId);
        builder.Property(message => message.ProviderMessageId).HasMaxLength(TextLimits.Description);
        builder.Property(message => message.LastErrorCode).HasMaxLength(TextLimits.ShortName);
        // ADR 0066: el HMAC del destino que ordena (nunca el destino en claro), la prioridad (0 contesta, 1 avisa) y el
        // plazo después del cual no se manda. Los dos índices sirven a la reserva: lo que toca por prioridad y espera, y lo
        // anterior de un mismo destino. Solo sobre lo que todavía puede salir.
        builder.Property(message => message.OrderingKey).HasMaxLength(OutboxMessage.OrderingKeyMaxLength);
        builder.Property(message => message.Priority).HasDefaultValue(OutboxPriority.Notice).HasSentinel((OutboxPriority)(-1));
        builder.HasIndex(message => new { message.Priority, message.NextAttemptAtUtc, message.Id })
            .HasDatabaseName("IX_OutboxMessages_Due").HasFilter(Claimable);
        builder.HasIndex(message => new { message.OrderingKey, message.CreatedAtUtc, message.Id })
            .HasDatabaseName("IX_OutboxMessages_Ordering").HasFilter(Claimable);
    }

    private const string Claimable = "\"Status\" IN ('Pending', 'Failed')";
}
