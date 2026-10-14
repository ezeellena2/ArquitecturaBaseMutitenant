using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppBotsAndTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PromotionsAskedAtUtc",
                schema: "tenant",
                table: "WhatsAppCustomers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformWhatsAppBots",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Hours = table.Column<string>(type: "jsonb", nullable: false),
                    Options = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Welcome = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    OutOfHours = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformWhatsAppBots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformWhatsAppTeams",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NotifyAfterMinutes = table.Column<int>(type: "integer", nullable: true),
                    MemberIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformWhatsAppTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTeams",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Inbox = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NotifyAfterMinutes = table.Column<int>(type: "integer", nullable: true),
                    MemberIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTeams", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppTeams_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppBots",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    AskPromotions = table.Column<bool>(type: "boolean", nullable: false),
                    OfferAccountLink = table.Column<bool>(type: "boolean", nullable: false),
                    EmployeesTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Hours = table.Column<string>(type: "jsonb", nullable: false),
                    Options = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Welcome = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    OutOfHours = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppBots", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppBots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppBots_WhatsAppChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "identity",
                        principalTable: "WhatsAppChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppBots_WhatsAppTeams_TenantId_EmployeesTeamId",
                        columns: x => new { x.TenantId, x.EmployeesTeamId },
                        principalSchema: "tenant",
                        principalTable: "WhatsAppTeams",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTeams_MemberIds",
                schema: "platform",
                table: "PlatformWhatsAppTeams",
                column: "MemberIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTeams_Name_Id",
                schema: "platform",
                table: "PlatformWhatsAppTeams",
                columns: new[] { "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTeams_NormalizedName",
                schema: "platform",
                table: "PlatformWhatsAppTeams",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBots_ChannelId",
                schema: "tenant",
                table: "WhatsAppBots",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBots_TenantId_ChannelId",
                schema: "tenant",
                table: "WhatsAppBots",
                columns: new[] { "TenantId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBots_TenantId_EmployeesTeamId",
                schema: "tenant",
                table: "WhatsAppBots",
                columns: new[] { "TenantId", "EmployeesTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTeams_MemberIds",
                schema: "tenant",
                table: "WhatsAppTeams",
                column: "MemberIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTeams_TenantId_Name_Id",
                schema: "tenant",
                table: "WhatsAppTeams",
                columns: new[] { "TenantId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTeams_TenantId_NormalizedName",
                schema: "tenant",
                table: "WhatsAppTeams",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppTeams");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppBots");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."WhatsAppTeams",
                tenant."WhatsAppBots",
                platform."PlatformWhatsAppTeams",
                platform."PlatformWhatsAppBots" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformWhatsAppBots",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformWhatsAppTeams",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "WhatsAppBots",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "WhatsAppTeams",
                schema: "tenant");

            migrationBuilder.DropColumn(
                name: "PromotionsAskedAtUtc",
                schema: "tenant",
                table: "WhatsAppCustomers");
        }
    }
}
