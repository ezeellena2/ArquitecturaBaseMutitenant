using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Configurations;

/// <summary>
/// Un catálogo de la organización (E12, §3): privado y con RLS. Los filtros van solo con miembros o contactos, que son siempre de
/// texto; los dos CHECK lo atan al origen. Un índice <c>(TenantId, campo, Id)</c> por cada columna que ordena el listado. Los campos
/// (E13), también los quitados, y lo que arma el título y la descripción en WhatsApp van en jsonb, con los valores por defecto en la
/// base: una fila que da de alta la versión anterior, que no manda esas columnas, queda sin campos y con la presentación de siempre
/// (0060).
/// </summary>
internal sealed class FormCatalogConfiguration : IEntityTypeConfiguration<FormCatalog>
{
    public void Configure(EntityTypeBuilder<FormCatalog> builder)
    {
        builder.ToTable("FormCatalogs", Schemas.Tenant, table =>
        {
            table.HasCheckConstraint("CK_FormCatalogs_Filters_Source",
                $"(\"Source\" = '{nameof(FormCatalogSource.Manual)}') = (\"Filters\" IS NULL)");
            table.HasCheckConstraint("CK_FormCatalogs_ValueType_Source",
                $"\"Source\" = '{nameof(FormCatalogSource.Manual)}' OR \"ValueType\" = '{nameof(FormCatalogValueType.Text)}'");
        });
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Name).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Description).HasMaxLength(FormsTextLimits.CatalogDescription);
        builder.Property(value => value.Source).HasConversion<string>().HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.ValueType).HasConversion<string>().HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Filters).HasColumnType("jsonb");
        builder.Property(value => value.FieldsJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(value => value.Presentation).HasColumnType("jsonb")
            .HasDefaultValueSql($"'{FormCatalogPresentation.Default.ToJson()}'::jsonb").IsRequired();
        builder.HasIndex(value => new { value.TenantId, value.Name, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.Source, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.ValueType, value.Id });
    }
}
