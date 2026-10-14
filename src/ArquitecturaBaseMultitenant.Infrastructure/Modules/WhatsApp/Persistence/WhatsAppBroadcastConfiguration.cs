using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Las difusiones de la organización: los valores de la plantilla van como jsonb y el costo estimado con su moneda.</summary>
internal sealed class WhatsAppBroadcastConfiguration : IEntityTypeConfiguration<WhatsAppBroadcast>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<WhatsAppBroadcast> builder)
    {
        builder.ToTable("WhatsAppBroadcasts", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Name).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.TemplateName).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property<List<string>>("_variables").HasColumnName("Variables").HasColumnType("jsonb").IsRequired()
            .HasConversion(value => JsonSerializer.Serialize(value, Json), value => JsonSerializer.Deserialize<List<string>>(value, Json) ?? new(),
                new ValueComparer<List<string>>((left, right) => left!.SequenceEqual(right!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)), value => value.ToList()));
        builder.Ignore(value => value.Variables);
        builder.Property(value => value.AudienceKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.AudienceTag).HasMaxLength(WhatsAppTextLimits.Tag);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.EstimatedCostAmount).HasPrecision(18, 6);
        builder.Property(value => value.EstimatedCostCurrency).HasColumnType("character(3)").IsRequired();
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.ReferenceData.Currency>().WithMany().HasForeignKey(value => value.EstimatedCostCurrency)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(value => value.EstimatedCost);
        builder.HasIndex(value => new { value.TenantId, value.Status, value.ScheduledAtUtc });
        builder.HasIndex(value => new { value.TenantId, value.Name, value.Id });
    }
}

/// <summary>Los destinatarios de cada difusión, con su envío en el outbox.</summary>
internal sealed class WhatsAppBroadcastRecipientConfiguration : IEntityTypeConfiguration<WhatsAppBroadcastRecipient>
{
    public void Configure(EntityTypeBuilder<WhatsAppBroadcastRecipient> builder)
    {
        builder.ToTable("WhatsAppBroadcastRecipients", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WhatsAppBroadcast>().WithMany().HasForeignKey(value => new { value.TenantId, value.BroadcastId })
            .HasPrincipalKey(value => new { value.TenantId, value.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Property(value => value.WaId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.FailureReason).HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => new { value.TenantId, value.BroadcastId, value.Status });
        builder.HasIndex(value => new { value.TenantId, value.CustomerId, value.SentAtUtc });
        builder.HasIndex(value => value.OutboxMessageId);
    }
}
