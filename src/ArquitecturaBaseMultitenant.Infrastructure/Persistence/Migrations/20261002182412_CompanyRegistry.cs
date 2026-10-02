using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TaxCountry = table.Column<string>(type: "character(2)", nullable: true),
                    TaxType = table.Column<string>(type: "text", nullable: true),
                    TaxNumber = table.Column<string>(type: "text", nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Companies_Countries_TaxCountry",
                        column: x => x.TaxCountry,
                        principalSchema: "platform",
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Companies_TaxIdTypes_TaxType",
                        column: x => x.TaxType,
                        principalSchema: "platform",
                        principalTable: "TaxIdTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Companies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Companies_TimeZones_TimeZoneId",
                        column: x => x.TimeZoneId,
                        principalSchema: "platform",
                        principalTable: "TimeZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "Companies");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TaxCountry",
                schema: "tenant",
                table: "Companies",
                column: "TaxCountry");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TaxType",
                schema: "tenant",
                table: "Companies",
                column: "TaxType");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_Name_Id",
                schema: "tenant",
                table: "Companies",
                columns: new[] { "TenantId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_TaxCountry_TaxType_TaxNumber",
                schema: "tenant",
                table: "Companies",
                columns: new[] { "TenantId", "TaxCountry", "TaxType", "TaxNumber" },
                unique: true,
                filter: "\"TaxCountry\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TimeZoneId",
                schema: "tenant",
                table: "Companies",
                column: "TimeZoneId");

            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON tenant.\"Companies\" TO mt_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies",
                schema: "tenant");
        }
    }
}
