using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvitationDestinationSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Búsqueda sin acentos de Usuarios y Miembros de empresa (MemberSearch): el destino de una invitación.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Invitations_Search_Destination" ON tenant."Invitations"
                    USING gin (public.f_unaccent(lower("Destination")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX tenant."IX_Invitations_Search_Destination";
                """);
        }
    }
}
