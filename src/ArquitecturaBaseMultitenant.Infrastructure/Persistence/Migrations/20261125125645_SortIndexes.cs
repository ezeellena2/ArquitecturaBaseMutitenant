using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SortIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status_Id",
                schema: "platform",
                table: "Tenants",
                columns: new[] { "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_IsSystemRole_Id",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "IsSystemRole", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Scope_Id",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "Scope", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_Status_Id",
                schema: "tenant",
                table: "Companies",
                columns: new[] { "TenantId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_TaxNumber_Id",
                schema: "tenant",
                table: "Companies",
                columns: new[] { "TenantId", "TaxNumber", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_TimeZoneId_Id",
                schema: "tenant",
                table: "Companies",
                columns: new[] { "TenantId", "TimeZoneId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryRequests_Status_Id",
                schema: "platform",
                table: "AccountRecoveryRequests",
                columns: new[] { "Status", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Status_Id",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_IsSystemRole_Id",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Scope_Id",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Companies_TenantId_Status_Id",
                schema: "tenant",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_TenantId_TaxNumber_Id",
                schema: "tenant",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_TenantId_TimeZoneId_Id",
                schema: "tenant",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_AccountRecoveryRequests_Status_Id",
                schema: "platform",
                table: "AccountRecoveryRequests");
        }
    }
}
