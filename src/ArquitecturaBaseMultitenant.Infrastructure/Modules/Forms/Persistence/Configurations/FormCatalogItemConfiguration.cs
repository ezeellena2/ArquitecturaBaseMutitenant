using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Configurations;

/// <summary>
/// Una opción de un catálogo a mano (E12, §3), con FK compuesta a su catálogo. El código y la posición son únicos por catálogo con
/// dos restricciones diferidas que crea la migración en SQL (<c>UniqueKeys</c>, C4): EF no sabe hacerlas diferidas. Un CHECK no
/// puede leer el tipo del catálogo, así que la base guarda a lo sumo un valor, y que sea el del tipo lo asegura el dominio bajo el
/// bloqueo del catálogo. Los valores de los campos (E13) van en jsonb, por clave, con <c>{}</c> por defecto en la base para la fila
/// que da de alta la versión anterior (0060), y un GIN <c>jsonb_path_ops</c> para encontrar con <c>@&gt;</c> las opciones que usan
/// un valor.
/// </summary>
internal sealed class FormCatalogItemConfiguration : IEntityTypeConfiguration<FormCatalogItem>
{
    public void Configure(EntityTypeBuilder<FormCatalogItem> builder)
    {
        builder.ToTable("FormCatalogItems", Schemas.Tenant, table =>
            table.HasCheckConstraint("CK_FormCatalogItems_SingleValue", "\"NumberValue\" IS NULL OR \"DateValue\" IS NULL"));
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FormCatalog>().WithMany().HasForeignKey(value => new { value.TenantId, value.CatalogId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(value => value.Code).HasMaxLength(FormsTextLimits.OptionCode).IsRequired();
        builder.Property(value => value.CodeNormalized).HasMaxLength(FormsTextLimits.OptionCode).IsRequired();
        builder.Property(value => value.Text).HasMaxLength(FormsTextLimits.OptionText).IsRequired();
        builder.Property(value => value.Detail).HasMaxLength(FormsTextLimits.OptionDetail);
        builder.Property(value => value.NumberValue).HasPrecision(FormCatalogItem.MaxIntegerDigits + FormCatalogItem.MaxDecimalDigits,
            FormCatalogItem.MaxDecimalDigits);
        builder.Property(value => value.FieldValuesJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.HasIndex(value => value.FieldValuesJson).HasMethod("gin").HasOperators("jsonb_path_ops");
    }
}
