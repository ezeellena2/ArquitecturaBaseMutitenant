using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditEntrySearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Búsqueda sin acentos de la auditoría de la organización (OrganizationAuditReader): la misma expresión de
            // ApplySearch sobre el tipo y sobre el texto de los cambios, que son jsonb.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_AuditEntries_Search_EntityType" ON tenant."AuditEntries"
                    USING gin (public.f_unaccent(lower("EntityType")) public.gin_trgm_ops);
                CREATE INDEX "IX_AuditEntries_Search_Changes" ON tenant."AuditEntries"
                    USING gin (public.f_unaccent(lower("Changes"::text)) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX tenant."IX_AuditEntries_Search_Changes";
                DROP INDEX tenant."IX_AuditEntries_Search_EntityType";
                """);
        }
    }
}
