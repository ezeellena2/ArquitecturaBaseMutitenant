using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CacheTagGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CacheTagVersions",
                schema: "platform",
                columns: table => new
                {
                    Tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheTagVersions", x => x.Tag);
                });

            // ADR 0052: la aplicación avanza las generaciones en la transacción que cambia los datos y las lee antes de
            // cada lectura de caché; nunca las borra, así una etiqueta no vuelve a una generación anterior.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE ON TABLE platform."CacheTagVersions" TO mt_app;
                """);

            // La identidad de esta base entra en toda clave: una base nueva o recreada no lee lo que otra dejó en Redis.
            migrationBuilder.Sql("""
                INSERT INTO platform."CacheTagVersions" ("Tag", "Generation")
                VALUES ('p:database', floor(random() * 9007199254740991)::bigint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CacheTagVersions",
                schema: "platform");
        }
    }
}
