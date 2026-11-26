using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0069: el permiso de uso de un módulo lleva una fila por origen, así que el índice único pasa de
    /// <c>(TenantId, FeatureKey)</c> a <c>(TenantId, FeatureKey, Source)</c>. Las filas guardadas quedan igual: el índice
    /// nuevo es más amplio que el viejo, y lo que el viejo admitía lo admite el nuevo. El writer de Plataforma toma un lock por
    /// organización y módulo y sigue dejando una sola fila. La 0060 pide que la versión anterior conviva con este esquema; sin
    /// ese lock, dos altas a la vez con distinto origen le dejarían dos filas que no sabe leer. Excepción anotada de esta etapa,
    /// como la base 2: todavía no hay despliegue (123), así que la versión anterior nunca convive con el índice nuevo. El
    /// <c>Down</c> se niega, antes de cualquier DDL, si un módulo tiene dos filas en una organización.
    /// </summary>
    public partial class TenantFeaturesBySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Primero el índice nuevo y después se quita el viejo: la tabla nunca queda sin unicidad.
            migrationBuilder.CreateIndex(
                name: "IX_TenantFeatures_TenantId_FeatureKey_Source",
                schema: "platform",
                table: "TenantFeatures",
                columns: new[] { "TenantId", "FeatureKey", "Source" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_TenantFeatures_TenantId_FeatureKey",
                schema: "platform",
                table: "TenantFeatures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dos filas de un mismo módulo no entran en el índice viejo, y elegir cuál borrar diría algo que no se sabe: se niega
            // antes de tocar el índice. El SQL va en una línea: no lleva \r aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE platform."TenantFeatures" IN ACCESS EXCLUSIVE MODE; DO $$ BEGIN IF EXISTS (SELECT 1 FROM platform."TenantFeatures" GROUP BY "TenantId", "FeatureKey" HAVING COUNT(*) > 1) THEN RAISE EXCEPTION 'A module with more than one row per organization cannot be represented by the previous schema.'; END IF; END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_TenantFeatures_TenantId_FeatureKey_Source",
                schema: "platform",
                table: "TenantFeatures");

            migrationBuilder.CreateIndex(
                name: "IX_TenantFeatures_TenantId_FeatureKey",
                schema: "platform",
                table: "TenantFeatures",
                columns: new[] { "TenantId", "FeatureKey" },
                unique: true);
        }
    }
}
