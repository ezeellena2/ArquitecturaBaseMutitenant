using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountDeletionMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // tenant_scope sigue restringiendo al alcance activo. Esta política restrictiva
            // impide DELETE en organizaciones B2B aun dentro de su propio alcance.
            migrationBuilder.Sql("""
                CREATE POLICY account_deletion_personal ON tenant."Members"
                AS RESTRICTIVE FOR DELETE TO mt_app USING (
                    EXISTS (SELECT 1 FROM platform."Tenants" t WHERE t."Id" = "TenantId" AND t."Kind" = 'Personal'));
                CREATE POLICY account_deletion_personal ON tenant."TenantSettings"
                AS RESTRICTIVE FOR DELETE TO mt_app USING (
                    EXISTS (SELECT 1 FROM platform."Tenants" t WHERE t."Id" = "TenantId" AND t."Kind" = 'Personal'));
                GRANT DELETE ON TABLE tenant."Members", tenant."TenantSettings" TO mt_app;
                """);
            migrationBuilder.AddColumn<string>(
                name: "RemovalReason",
                schema: "tenant",
                table: "Members",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                REVOKE DELETE ON TABLE tenant."Members", tenant."TenantSettings" FROM mt_app;
                DROP POLICY account_deletion_personal ON tenant."Members";
                DROP POLICY account_deletion_personal ON tenant."TenantSettings";
                """);
            migrationBuilder.DropColumn(
                name: "RemovalReason",
                schema: "tenant",
                table: "Members");
        }
    }
}
