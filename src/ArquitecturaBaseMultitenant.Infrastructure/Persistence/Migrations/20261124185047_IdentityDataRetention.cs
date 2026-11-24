using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0062: la retención de los datos del ingreso. Los índices que recorre cada purga por fecha (los códigos por su
    /// creación, y las invitaciones cerradas de cada organización por la fecha en que se mandaron), el del alta de una
    /// invitación en la auditoría, que nombra a la persona de una invitación ya purgada, y el permiso de borrar los ingresos
    /// y las invitaciones, que mt_app no tenía: borra cada una en su alcance, con su RLS, sin un rol que lo saltee. Los
    /// ingresos ya tenían su índice por cuenta y fecha, y por fecha e Id. Sin columnas nuevas; solo agrega, así que también
    /// cumple la 0060 (excepción anotada de esta etapa: todavía no hay despliegue, 123).
    /// </summary>
    public partial class IdentityDataRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LoginCodes_CreatedAtUtc_Id",
                schema: "identity",
                table: "LoginCodes",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Closed",
                schema: "tenant",
                table: "Invitations",
                columns: new[] { "TenantId", "IssuedAtUtc", "Id" },
                filter: "\"Status\" <> 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_InvitationCreated",
                schema: "tenant",
                table: "AuditEntries",
                columns: new[] { "TenantId", "EntityId" },
                filter: "\"EntityType\" = 'Invitation' AND \"Action\" = 'Created'");

            migrationBuilder.Sql("""
                GRANT DELETE ON TABLE identity."LoginAudits" TO mt_app;
                GRANT DELETE ON TABLE tenant."Invitations" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                REVOKE DELETE ON TABLE identity."LoginAudits" FROM mt_app;
                REVOKE DELETE ON TABLE tenant."Invitations" FROM mt_app;
                """);

            migrationBuilder.DropIndex(
                name: "IX_LoginCodes_CreatedAtUtc_Id",
                schema: "identity",
                table: "LoginCodes");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_Closed",
                schema: "tenant",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_InvitationCreated",
                schema: "tenant",
                table: "AuditEntries");
        }
    }
}
