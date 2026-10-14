using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>El vínculo de una conversación con una cuenta: compartido entre la persona y la organización, uno por conversación.</summary>
internal sealed class WhatsAppConversationLinkConfiguration : IEntityTypeConfiguration<WhatsAppConversationLink>
{
    public void Configure(EntityTypeBuilder<WhatsAppConversationLink> builder)
    {
        builder.ToTable("WhatsAppConversationLinks", Schemas.Engagement);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.OrganizationName).HasMaxLength(TextLimits.OrganizationName).IsRequired();
        builder.Property(value => value.DisplayNumber).HasMaxLength(PhoneNumber.MaxLength).IsRequired();
        builder.Property(value => value.ConsumerName).HasMaxLength(TextLimits.PersonDisplayName);
        builder.Property(value => value.LastMessagePreview).HasMaxLength(WhatsAppChatBase.PreviewLength);
        builder.HasIndex(value => new { value.BusinessTenantId, value.ChatId }).IsUnique();
        builder.HasIndex(value => new { value.ConsumerTenantId, value.LastMessageAtUtc, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.ConsumerTenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.BusinessTenantId)
            .OnDelete(DeleteBehavior.Restrict);
        // Se borra con la conversación (que se borra con su contacto, D4).
        builder.HasOne<WhatsAppChat>().WithMany().HasForeignKey(value => new { value.BusinessTenantId, value.ChatId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
