using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyMemberships",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMemberships", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CompanyMemberships_Companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyMemberships_Members_TenantId_MemberId",
                        columns: x => new { x.TenantId, x.MemberId },
                        principalSchema: "tenant",
                        principalTable: "Members",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "CompanyMemberships");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_CompanyId",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_CompanyId",
                schema: "tenant",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMemberships_TenantId_CompanyId_MemberId",
                schema: "tenant",
                table: "CompanyMemberships",
                columns: new[] { "TenantId", "CompanyId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMemberships_TenantId_MemberId",
                schema: "tenant",
                table: "CompanyMemberships",
                columns: new[] { "TenantId", "MemberId" });

            migrationBuilder.AddForeignKey(
                name: "FK_RoleAssignments_Companies_TenantId_CompanyId",
                schema: "tenant",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "CompanyId" },
                principalSchema: "tenant",
                principalTable: "Companies",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Companies_TenantId_CompanyId",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "CompanyId" },
                principalSchema: "tenant",
                principalTable: "Companies",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON tenant.\"CompanyMemberships\" TO mt_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoleAssignments_Companies_TenantId_CompanyId",
                schema: "tenant",
                table: "RoleAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Companies_TenantId_CompanyId",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.DropTable(
                name: "CompanyMemberships",
                schema: "tenant");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_CompanyId",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RoleAssignments_TenantId_CompanyId",
                schema: "tenant",
                table: "RoleAssignments");
        }
    }
}
