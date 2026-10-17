using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppTeamNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformWhatsAppTeamNotices",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformWhatsAppTeamNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformWhatsAppTeamNotices_PlatformWhatsAppTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "platform",
                        principalTable: "PlatformWhatsAppTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTeamNotices",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTeamNotices", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppTeamNotices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppTeamNotices_WhatsAppTeams_TenantId_TeamId",
                        columns: x => new { x.TenantId, x.TeamId },
                        principalSchema: "tenant",
                        principalTable: "WhatsAppTeams",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTeamNotices_TeamId",
                schema: "platform",
                table: "PlatformWhatsAppTeamNotices",
                column: "TeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTeamNotices_TenantId_TeamId",
                schema: "tenant",
                table: "WhatsAppTeamNotices",
                columns: new[] { "TenantId", "TeamId" },
                unique: true);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppTeamNotices");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."WhatsAppTeamNotices",
                platform."PlatformWhatsAppTeamNotices" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformWhatsAppTeamNotices",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "WhatsAppTeamNotices",
                schema: "tenant");
        }
    }
}
