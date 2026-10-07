using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicDirectoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_Name_Id",
                schema: "public_site",
                table: "PublicPages",
                columns: new[] { "Name", "Id" });

            migrationBuilder.Sql("""
                CREATE INDEX "IX_PublicPages_Search_Name" ON public_site."PublicPages"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX public_site."IX_PublicPages_Search_Name";
                """);

            migrationBuilder.DropIndex(
                name: "IX_PublicPages_Name_Id",
                schema: "public_site",
                table: "PublicPages");
        }
    }
}
