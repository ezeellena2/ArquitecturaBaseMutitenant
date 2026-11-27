using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Formularios, E12 (§3 de la spec): los catálogos, sus opciones y la barrera de las operaciones de archivo, privados de la
    /// organización, con RLS y el GRANT a <c>mt_app</c>. El código y la posición de una opción son únicos por catálogo con dos
    /// restricciones diferidas (C4), que EF no sabe hacer: van en SQL, cada sentencia en una línea, sin <c>\r</c> aunque el archivo
    /// tenga CRLF. La búsqueda sin acentos tiene sus índices trigram, y cada columna del orden del listado, su índice del modelo.
    /// </summary>
    public partial class FormCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormCatalogs",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Filters = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormCatalogs", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_FormCatalogs_Filters_Source", "(\"Source\" = 'Manual') = (\"Filters\" IS NULL)");
                    table.CheckConstraint("CK_FormCatalogs_ValueType_Source", "\"Source\" = 'Manual' OR \"ValueType\" = 'Text'");
                    table.ForeignKey(
                        name: "FK_FormCatalogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormWorkLeases",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormWorkLeases", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FormWorkLeases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormCatalogItems",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CodeNormalized = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    DateValue = table.Column<DateOnly>(type: "date", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormCatalogItems", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_FormCatalogItems_SingleValue", "\"NumberValue\" IS NULL OR \"DateValue\" IS NULL");
                    table.ForeignKey(
                        name: "FK_FormCatalogItems_FormCatalogs_TenantId_CatalogId",
                        columns: x => new { x.TenantId, x.CatalogId },
                        principalSchema: "tenant",
                        principalTable: "FormCatalogs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormCatalogItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormCatalogItems_TenantId_CatalogId",
                schema: "tenant",
                table: "FormCatalogItems",
                columns: new[] { "TenantId", "CatalogId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormCatalogs_TenantId_Name_Id",
                schema: "tenant",
                table: "FormCatalogs",
                columns: new[] { "TenantId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FormCatalogs_TenantId_Source_Id",
                schema: "tenant",
                table: "FormCatalogs",
                columns: new[] { "TenantId", "Source", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FormCatalogs_TenantId_ValueType_Id",
                schema: "tenant",
                table: "FormCatalogs",
                columns: new[] { "TenantId", "ValueType", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FormWorkLeases_TenantId_UserId",
                schema: "tenant",
                table: "FormWorkLeases",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            // C4: el código y la posición, únicos por catálogo y diferidos, así un guardado intercambia sin chocar a mitad de camino.
            migrationBuilder.Sql("""
                ALTER TABLE tenant."FormCatalogItems" ADD CONSTRAINT "AK_FormCatalogItems_TenantId_CatalogId_CodeNormalized" UNIQUE ("TenantId", "CatalogId", "CodeNormalized") DEFERRABLE INITIALLY DEFERRED;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE tenant."FormCatalogItems" ADD CONSTRAINT "AK_FormCatalogItems_TenantId_CatalogId_Position" UNIQUE ("TenantId", "CatalogId", "Position") DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "FormCatalogs");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "FormCatalogItems");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "FormWorkLeases");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE tenant."FormCatalogs", tenant."FormCatalogItems", tenant."FormWorkLeases" TO mt_app;
                """);

            // Búsqueda sin acentos por el nombre del catálogo y por el texto o el código de una opción (paginado-y-busqueda).
            migrationBuilder.Sql("""
                CREATE INDEX "IX_FormCatalogs_Search_Name" ON tenant."FormCatalogs" USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                """);
            migrationBuilder.Sql("""
                CREATE INDEX "IX_FormCatalogItems_Search_Text" ON tenant."FormCatalogItems" USING gin (public.f_unaccent(lower("Text")) public.gin_trgm_ops);
                """);
            migrationBuilder.Sql("""
                CREATE INDEX "IX_FormCatalogItems_Search_Code" ON tenant."FormCatalogItems" USING gin (public.f_unaccent(lower("Code")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un catálogo, con sus opciones, se perdería: se niega antes que borrarlo, antes de cualquier DDL (guía de migración,
            // paso 4). FormCatalogs fuerza la RLS también para su dueño, que corre la migración sin organización: sin NO FORCE la
            // guarda no vería ningún catálogo. Va en la transacción de la migración: si se niega, la RLS sigue forzada. Una opción
            // no existe sin su catálogo (FK), y la barrera de archivos no se guarda: vence sola a los dos minutos.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."FormCatalogs", tenant."FormCatalogItems" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."FormCatalogs" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."FormCatalogs") THEN RAISE EXCEPTION 'A form catalog cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."FormCatalogs" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "FormCatalogItems",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "FormWorkLeases",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "FormCatalogs",
                schema: "tenant");
        }
    }
}
