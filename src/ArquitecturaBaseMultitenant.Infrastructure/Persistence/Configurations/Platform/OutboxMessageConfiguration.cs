using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

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
    }
}
