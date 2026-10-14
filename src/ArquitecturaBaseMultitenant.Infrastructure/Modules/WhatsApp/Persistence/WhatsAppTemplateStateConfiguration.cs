using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Lo que Meta dice de cada plantilla por cuenta de WhatsApp: global, como los números; uno por nombre e idioma.</summary>
internal sealed class WhatsAppTemplateStateConfiguration : IEntityTypeConfiguration<WhatsAppTemplateState>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplateState> builder)
    {
        builder.ToTable("WhatsAppTemplateStates", Schemas.Identity);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.WabaId).HasMaxLength(WhatsAppTextLimits.MetaIdentifier).IsRequired();
        builder.Property(value => value.MetaName).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.MetaLanguage).HasMaxLength(TextLimits.ShortCode).IsRequired();
        builder.Property(value => value.MetaTemplateId).HasMaxLength(WhatsAppTextLimits.MetaIdentifier);
        builder.Property(value => value.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Category).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.RejectionReason).HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => new { value.WabaId, value.MetaName, value.MetaLanguage }).IsUnique().HasDatabaseName(WhatsAppUniqueKeys.TemplateState);
    }
}

/// <summary>Las tarifas de Meta: dato de referencia de Plataforma, con FK al país y a la moneda.</summary>
internal sealed class WhatsAppRateConfiguration : IEntityTypeConfiguration<WhatsAppRate>
{
    public void Configure(EntityTypeBuilder<WhatsAppRate> builder)
    {
        builder.ToTable("WhatsAppRates", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CountryCode).HasColumnType("character(2)").IsRequired();
        builder.HasOne<Country>().WithMany().HasForeignKey(value => value.CountryCode).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Category).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Amount).HasPrecision(18, 6);
        builder.Property(value => value.CurrencyCode).HasColumnType("character(3)").IsRequired();
        builder.HasOne<Currency>().WithMany().HasForeignKey(value => value.CurrencyCode).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(value => value.Price);
        builder.HasIndex(value => new { value.CountryCode, value.Category, value.EffectiveFrom }).IsUnique();
    }
}
