using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RolesAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Scope = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Roles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "Roles");

            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Members_TenantId_MemberId",
                        columns: x => new { x.TenantId, x.MemberId },
                        principalSchema: "tenant",
                        principalTable: "Members",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalSchema: "tenant",
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "RoleAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_MemberId_RoleId",
                schema: "tenant",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "MemberId", "RoleId" },
                unique: true,
                filter: "\"CompanyId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_MemberId_RoleId_CompanyId",
                schema: "tenant",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "MemberId", "RoleId", "CompanyId" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId_RoleId_MemberId_Id",
                schema: "tenant",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "RoleId", "MemberId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name_Id",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "Name", "Id" });
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON tenant.\"Roles\", tenant.\"RoleAssignments\" TO mt_app;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleAssignments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "tenant");
        }
    }
}
