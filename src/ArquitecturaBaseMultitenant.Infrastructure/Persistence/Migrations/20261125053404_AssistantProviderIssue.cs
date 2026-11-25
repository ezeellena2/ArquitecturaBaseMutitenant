using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0064 §7 (B13): la última falla del proveedor que arregla quien configura el asistente, y cuándo pasó, en su propia fila:
    /// privada para la clave propia de cada organización y global para la clave de la plataforma. No van en las configuraciones,
    /// que son versionadas: el worker la anota después de cada turno, y en la misma fila le cambiaría la versión a quien la está
    /// editando. Son tablas nuevas: una versión anterior las ignora. Se escriben en el mismo despliegue que las lee, como excepción
    /// anotada de la 0060 en esta etapa (todavía no hay despliegue, 123).
    /// </summary>
    public partial class AssistantProviderIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantOrganizationProviderIssues",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantOrganizationProviderIssues", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantOrganizationProviderIssues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantPlatformProviderIssues",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantPlatformProviderIssues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantOrganizationProviderIssues_TenantId",
                schema: "tenant",
                table: "AssistantOrganizationProviderIssues",
                column: "TenantId",
                unique: true);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantOrganizationProviderIssues");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                    platform."AssistantPlatformProviderIssues", tenant."AssistantOrganizationProviderIssues"
                TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantOrganizationProviderIssues",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssistantPlatformProviderIssues",
                schema: "platform");
        }
    }
}
