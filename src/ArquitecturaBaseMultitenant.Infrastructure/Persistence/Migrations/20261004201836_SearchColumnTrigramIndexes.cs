using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SearchColumnTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Búsqueda sin acentos (ApplySearch) de las columnas que todavía no tenían su índice: Roles, Empresas,
            // auditoría de Plataforma, el correo de contacto de Google en Cuentas y Recuperaciones.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Roles_Search_Name" ON tenant."Roles"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                CREATE INDEX "IX_Roles_Search_Description" ON tenant."Roles"
                    USING gin (public.f_unaccent(lower("Description")) public.gin_trgm_ops);
                CREATE INDEX "IX_Companies_Search_Name" ON tenant."Companies"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                CREATE INDEX "IX_Companies_Search_TaxNumber" ON tenant."Companies"
                    USING gin (public.f_unaccent(lower("TaxNumber")) public.gin_trgm_ops);
                CREATE INDEX "IX_SecurityEvents_Search_Reason" ON platform."SecurityEvents"
                    USING gin (public.f_unaccent(lower("Reason")) public.gin_trgm_ops);
                CREATE INDEX "IX_LoginMethods_Search_ContactEmail" ON identity."LoginMethods"
                    USING gin (public.f_unaccent(lower("ContactEmail")) public.gin_trgm_ops);
                CREATE INDEX "IX_AccountRecoveryRequests_Search_PreviousMethodValue" ON platform."AccountRecoveryRequests"
                    USING gin (public.f_unaccent(lower("PreviousMethodValue")) public.gin_trgm_ops);
                CREATE INDEX "IX_AccountRecoveryRequests_Search_NewMethodValue" ON platform."AccountRecoveryRequests"
                    USING gin (public.f_unaccent(lower("NewMethodValue")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX platform."IX_AccountRecoveryRequests_Search_NewMethodValue";
                DROP INDEX platform."IX_AccountRecoveryRequests_Search_PreviousMethodValue";
                DROP INDEX identity."IX_LoginMethods_Search_ContactEmail";
                DROP INDEX platform."IX_SecurityEvents_Search_Reason";
                DROP INDEX tenant."IX_Companies_Search_TaxNumber";
                DROP INDEX tenant."IX_Companies_Search_Name";
                DROP INDEX tenant."IX_Roles_Search_Description";
                DROP INDEX tenant."IX_Roles_Search_Name";
                """);
        }
    }
}
