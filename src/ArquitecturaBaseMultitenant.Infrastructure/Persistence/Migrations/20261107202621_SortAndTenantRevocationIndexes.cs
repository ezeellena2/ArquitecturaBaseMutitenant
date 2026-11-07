using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SortAndTenantRevocationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Members_TenantId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "Members",
                columns: new[] { "TenantId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "Companies",
                columns: new[] { "TenantId", "CreatedAtUtc", "Id" });

            // Revocar una organización (TokenRevoker.RevokeTenantAsync) busca sus autorizaciones por el tenant_id que
            // guarda ConnectAuthorizationStore en las propiedades, con esta misma expresión.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_OpenIddictAuthorizations_TenantId" ON platform."OpenIddictAuthorizations"
                    (("Properties"::jsonb ->> 'tenant_id'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX platform."IX_OpenIddictAuthorizations_TenantId";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Members_TenantId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Companies_TenantId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "Companies");
        }
    }
}
