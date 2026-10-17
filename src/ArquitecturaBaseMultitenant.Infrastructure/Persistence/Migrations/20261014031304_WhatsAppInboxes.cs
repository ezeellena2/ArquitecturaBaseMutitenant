using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppInboxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_OutboxMessageId",
                schema: "identity",
                table: "WhatsAppMessages",
                column: "OutboxMessageId",
                filter: "\"OutboxMessageId\" IS NOT NULL");

            // Búsquedas de atención sin acentos (paginado-y-busqueda): contactos y números de las bandejas, y equipos.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_WhatsAppCustomers_Search_Name" ON tenant."WhatsAppCustomers"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                CREATE INDEX "IX_WhatsAppCustomers_Search_WaId" ON tenant."WhatsAppCustomers"
                    USING gin (public.f_unaccent(lower("WaId")) public.gin_trgm_ops);
                CREATE INDEX "IX_WhatsAppChats_Search_WaId" ON tenant."WhatsAppChats"
                    USING gin (public.f_unaccent(lower("WaId")) public.gin_trgm_ops);
                CREATE INDEX "IX_PlatformWhatsAppChats_Search_WaId" ON platform."PlatformWhatsAppChats"
                    USING gin (public.f_unaccent(lower("WaId")) public.gin_trgm_ops);
                CREATE INDEX "IX_WhatsAppTeams_Search_Name" ON tenant."WhatsAppTeams"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                CREATE INDEX "IX_PlatformWhatsAppTeams_Search_Name" ON platform."PlatformWhatsAppTeams"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX platform."IX_PlatformWhatsAppTeams_Search_Name";
                DROP INDEX tenant."IX_WhatsAppTeams_Search_Name";
                DROP INDEX platform."IX_PlatformWhatsAppChats_Search_WaId";
                DROP INDEX tenant."IX_WhatsAppChats_Search_WaId";
                DROP INDEX tenant."IX_WhatsAppCustomers_Search_WaId";
                DROP INDEX tenant."IX_WhatsAppCustomers_Search_Name";
                """);

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppMessages_OutboxMessageId",
                schema: "identity",
                table: "WhatsAppMessages");
        }
    }
}
