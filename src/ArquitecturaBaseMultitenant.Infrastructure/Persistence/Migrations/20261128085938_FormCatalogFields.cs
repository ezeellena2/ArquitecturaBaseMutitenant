using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FormCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldsJson",
                schema: "tenant",
                table: "FormCatalogs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Presentation",
                schema: "tenant",
                table: "FormCatalogs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{\"title\":[\"text\"],\"description\":[\"detail\"]}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "FieldValuesJson",
                schema: "tenant",
                table: "FormCatalogItems",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_FormCatalogItems_FieldValuesJson",
                schema: "tenant",
                table: "FormCatalogItems",
                column: "FieldValuesJson")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un campo, un valor o una presentación que no es la de siempre no tienen lugar en el esquema anterior: se niega antes que
            // perderlos. Las dos tablas fuerzan la RLS también para su dueño, que corre la migración sin organización: la guarda mira
            // entre NO FORCE y FORCE, en la transacción de la migración. El SQL va en una sola línea.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."FormCatalogs", tenant."FormCatalogItems" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."FormCatalogs" NO FORCE ROW LEVEL SECURITY; ALTER TABLE tenant."FormCatalogItems" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."FormCatalogs" WHERE "FieldsJson" <> '[]'::jsonb OR "Presentation" <> '{"title":["text"],"description":["detail"]}'::jsonb) OR EXISTS (SELECT 1 FROM tenant."FormCatalogItems" WHERE "FieldValuesJson" <> '{}'::jsonb) THEN RAISE EXCEPTION 'A form catalog field or presentation cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."FormCatalogs" FORCE ROW LEVEL SECURITY; ALTER TABLE tenant."FormCatalogItems" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropIndex(
                name: "IX_FormCatalogItems_FieldValuesJson",
                schema: "tenant",
                table: "FormCatalogItems");

            migrationBuilder.DropColumn(
                name: "FieldsJson",
                schema: "tenant",
                table: "FormCatalogs");

            migrationBuilder.DropColumn(
                name: "Presentation",
                schema: "tenant",
                table: "FormCatalogs");

            migrationBuilder.DropColumn(
                name: "FieldValuesJson",
                schema: "tenant",
                table: "FormCatalogItems");
        }
    }
}
