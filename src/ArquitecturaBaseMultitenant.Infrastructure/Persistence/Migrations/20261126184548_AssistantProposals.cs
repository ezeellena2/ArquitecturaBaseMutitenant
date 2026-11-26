using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>La propuesta de las acciones del asistente (ADR 0068 §8): tabla nueva, aditiva, encima de la de B4.</summary>
    public partial class AssistantProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantProposals",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ActionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActionVersion = table.Column<int>(type: "integer", nullable: false),
                    Values = table.Column<string>(type: "jsonb", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    BaseVersion = table.Column<long>(type: "bigint", nullable: true),
                    State = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AttemptKeys = table.Column<string>(type: "jsonb", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedLocation = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantProposals", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantProposals_AssistantMessages_TenantId_ConversationI~",
                        columns: x => new { x.TenantId, x.ConversationId, x.MessageId },
                        principalSchema: "tenant",
                        principalTable: "AssistantMessages",
                        principalColumns: new[] { "TenantId", "ConversationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantProposals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ADR 0068 §8: privada de su dueño, con RLS forzado como los mensajes.
            migrationBuilder.EnableTenantRls("tenant", "AssistantProposals");
            // La Api corre como mt_app, como en las demás tablas del asistente.
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE tenant.\"AssistantProposals\" TO mt_app;");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantProposals_TenantId_ConversationId_Kind",
                schema: "tenant",
                table: "AssistantProposals",
                columns: new[] { "TenantId", "ConversationId", "Kind" },
                filter: "\"State\" = 'New'");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantProposals_TenantId_ConversationId_MessageId",
                schema: "tenant",
                table: "AssistantProposals",
                columns: new[] { "TenantId", "ConversationId", "MessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Una propuesta guardada (nueva, aplicada, con lo aplicado o con las claves de sus intentos) no existe en el esquema
            // de antes: se niega antes que borrarla, antes de cualquier DDL (guía de migración, paso 4). AssistantProposals
            // fuerza la RLS también para su dueño, que corre la migración sin organización: sin NO FORCE la guarda no vería
            // ninguna. Va en la transacción de la migración: si se niega, la RLS sigue forzada. El SQL va en una línea: no lleva
            // \r aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."AssistantProposals" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."AssistantProposals" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."AssistantProposals") THEN RAISE EXCEPTION 'A saved assistant proposal cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."AssistantProposals" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "AssistantProposals",
                schema: "tenant");
        }
    }
}
