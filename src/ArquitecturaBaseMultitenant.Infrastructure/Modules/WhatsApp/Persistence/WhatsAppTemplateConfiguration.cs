using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Las plantillas de la organización: un nombre de Meta por organización.</summary>
internal sealed class WhatsAppTemplateConfiguration : IEntityTypeConfiguration<WhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplate> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("WhatsAppTemplates", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        WhatsAppTemplateMapping.Configure(builder);
        builder.HasIndex(value => new { value.TenantId, value.MetaName }).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.TemplateName);
        builder.HasIndex(value => new { value.TenantId, value.Name, value.Id });
    }
}

/// <summary>Las plantillas «Para consultas» de Plataforma.</summary>
internal sealed class PlatformWhatsAppTemplateConfiguration : IEntityTypeConfiguration<PlatformWhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<PlatformWhatsAppTemplate> builder)
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable("PlatformWhatsAppTemplates", Schemas.Platform);
        builder.HasKey(value => value.Id);
        WhatsAppTemplateMapping.Configure(builder);
        builder.HasIndex(value => value.MetaName).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.PlatformTemplateName);
        builder.HasIndex(value => new { value.Name, value.Id });
    }
}

/// <summary>Lo común de las plantillas: los datos van como jsonb (se leen y se guardan enteros) y la cultura, con FK.</summary>
internal static class WhatsAppTemplateMapping
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static void Configure<T>(EntityTypeBuilder<T> builder) where T : WhatsAppTemplateBase
    {
        builder.Property(value => value.Name).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.MetaName).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Language).HasMaxLength(TextLimits.ShortCode).IsRequired();
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.ReferenceData.Culture>().WithMany().HasForeignKey(value => value.Language).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.MetaLanguage).HasMaxLength(TextLimits.ShortCode).IsRequired();
        builder.Property(value => value.Category).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Body).HasMaxLength(WhatsAppTextLimits.TemplateBody).IsRequired();
        builder.Property<List<WhatsAppTemplateVariable>>("_variables").HasColumnName("Variables").HasColumnType("jsonb").IsRequired()
            .HasConversion(value => JsonSerializer.Serialize(value, Json), value => JsonSerializer.Deserialize<List<WhatsAppTemplateVariable>>(value, Json) ?? new(),
                new ValueComparer<List<WhatsAppTemplateVariable>>((left, right) => left!.SequenceEqual(right!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)), value => value.ToList()));
        builder.Ignore(value => value.Variables);
    }
}
