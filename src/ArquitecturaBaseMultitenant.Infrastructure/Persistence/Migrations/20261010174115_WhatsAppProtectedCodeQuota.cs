using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppProtectedCodeQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacyReservedCount",
                schema: "identity",
                table: "WhatsAppCodeQuotas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NewReservedCount",
                schema: "identity",
                table: "WhatsAppCodeQuotas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // No hay propósito/IP históricos: conservar todo el gasto como nuevo y desconocido.
            migrationBuilder.Sql("""
                UPDATE identity."WhatsAppCodeQuotas"
                SET "NewReservedCount" = "ReservedCount", "LegacyReservedCount" = "ReservedCount";
                """);

            migrationBuilder.CreateTable(
                name: "WhatsAppCodeQuotaIps",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ReservedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppCodeQuotaIps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppCodeQuotaIps_WhatsAppCodeQuotas_QuotaId",
                        column: x => x.QuotaId,
                        principalSchema: "identity",
                        principalTable: "WhatsAppCodeQuotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCodeQuotaIps_QuotaId_IpKey",
                schema: "identity",
                table: "WhatsAppCodeQuotaIps",
                columns: new[] { "QuotaId", "IpKey" },
                unique: true);
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity.\"WhatsAppCodeQuotaIps\" TO mt_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppCodeQuotaIps",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "LegacyReservedCount",
                schema: "identity",
                table: "WhatsAppCodeQuotas");

            migrationBuilder.DropColumn(
                name: "NewReservedCount",
                schema: "identity",
                table: "WhatsAppCodeQuotas");
        }
    }
}
