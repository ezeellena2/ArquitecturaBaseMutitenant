using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleNamePerCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_CompanyId",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_NormalizedName",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_CompanyId_NormalizedName",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "CompanyId", "NormalizedName" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_CompanyId_NormalizedName",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_CompanyId",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_NormalizedName",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);
        }
    }
}
