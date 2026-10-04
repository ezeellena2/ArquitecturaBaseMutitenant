using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingTenantOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingTenantOwners",
                schema: "platform",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingTenantOwners", x => x.InvitationId);
                    table.ForeignKey(
                        name: "FK_PendingTenantOwners_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingTenantOwners_TenantId",
                schema: "platform",
                table: "PendingTenantOwners",
                column: "TenantId");

            // Runtime solo agrega y lee. La copia del correo se borra cuando la invitación deja de estar pendiente
            // (aceptada, revocada o eliminada), también desde la baja de la cuenta, sin que Plataforma lea Invitations.
            migrationBuilder.Sql("""
                CREATE FUNCTION platform.forget_pending_tenant_owner() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                BEGIN
                    DELETE FROM platform."PendingTenantOwners"
                    WHERE "InvitationId" = CASE WHEN TG_OP = 'DELETE' THEN OLD."Id" ELSE NEW."Id" END;
                    RETURN NULL;
                END
                $function$;
                REVOKE ALL ON FUNCTION platform.forget_pending_tenant_owner() FROM PUBLIC;
                CREATE TRIGGER forget_pending_tenant_owner_on_status AFTER UPDATE OF "Status" ON tenant."Invitations"
                    FOR EACH ROW WHEN (NEW."Status" <> 'Pending') EXECUTE FUNCTION platform.forget_pending_tenant_owner();
                CREATE TRIGGER forget_pending_tenant_owner_on_delete AFTER DELETE ON tenant."Invitations"
                    FOR EACH ROW EXECUTE FUNCTION platform.forget_pending_tenant_owner();
                GRANT SELECT, INSERT ON platform."PendingTenantOwners" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER forget_pending_tenant_owner_on_delete ON tenant."Invitations";
                DROP TRIGGER forget_pending_tenant_owner_on_status ON tenant."Invitations";
                DROP FUNCTION platform.forget_pending_tenant_owner();
                """);

            migrationBuilder.DropTable(
                name: "PendingTenantOwners",
                schema: "platform");
        }
    }
}
