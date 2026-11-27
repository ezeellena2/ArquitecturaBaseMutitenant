using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Configurations;

/// <summary>
/// Un catálogo de la organización (E12, §3): privado y con RLS. Los filtros van solo con miembros o contactos, que son siempre de
/// texto; los dos CHECK lo atan al origen. Un índice <c>(TenantId, campo, Id)</c> por cada columna que ordena el listado.
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
        builder.HasIndex(value => new { value.TenantId, value.Name, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.Source, value.Id });
        builder.HasIndex(value => new { value.TenantId, value.ValueType, value.Id });
    }
}
