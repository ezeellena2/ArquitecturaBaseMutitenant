using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformAccountSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DisplayName_Id",
                schema: "identity",
                table: "AspNetUsers",
                columns: new[] { "DisplayName", "Id" });

            // Búsqueda sin acentos de Plataforma: cualquier correo o teléfono de una cuenta y el Dueño invitado.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_LoginMethods_Search_Value" ON identity."LoginMethods"
                    USING gin (public.f_unaccent(lower("Value")) public.gin_trgm_ops);
                CREATE INDEX "IX_PendingTenantOwners_Search_Address" ON platform."PendingTenantOwners"
                    USING gin (public.f_unaccent(lower("Address")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX platform."IX_PendingTenantOwners_Search_Address";
                DROP INDEX identity."IX_LoginMethods_Search_Value";
                """);

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DisplayName_Id",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
