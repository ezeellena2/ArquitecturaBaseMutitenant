using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountRecoveryNoticeReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountRecoveryRequests_CreatedAtUtc_Id",
                schema: "platform",
                table: "AccountRecoveryRequests");

            migrationBuilder.CreateTable(
                name: "AccountRecoveryNotices",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRecoveryNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRecoveryNotices_AccountRecoveryRequests_Id",
                        column: x => x.Id,
                        principalSchema: "platform",
                        principalTable: "AccountRecoveryRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Conserva las marcas ya confirmadas: el worker no reenvía avisos históricos tras el upgrade.
            migrationBuilder.Sql("""
                INSERT INTO platform."AccountRecoveryNotices" ("Id", "ResolvedAtUtc")
                SELECT "Id", "AccountNotifiedAtUtc" FROM platform."AccountRecoveryRequests"
                WHERE "AccountNotifiedAtUtc" IS NOT NULL;
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform."AccountRecoveryNotices" TO mt_app;
                """);

            migrationBuilder.DropColumn(
                name: "AccountNotifiedAtUtc",
                schema: "platform",
                table: "AccountRecoveryRequests");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryRequests_CreatedAtUtc_Id",
                schema: "platform",
                table: "AccountRecoveryRequests",
                columns: new[] { "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountRecoveryRequests_CreatedAtUtc_Id",
                schema: "platform",
                table: "AccountRecoveryRequests");

            migrationBuilder.AddColumn<DateTime>(
                name: "AccountNotifiedAtUtc",
                schema: "platform",
                table: "AccountRecoveryRequests",
                type: "timestamp with time zone",
                nullable: true);

            // También el downgrade conserva avisos resueltos y deja null únicamente los pendientes.
            migrationBuilder.Sql("""
                UPDATE platform."AccountRecoveryRequests" AS request
                SET "AccountNotifiedAtUtc" = notice."ResolvedAtUtc"
                FROM platform."AccountRecoveryNotices" AS notice WHERE notice."Id" = request."Id";
                """);

            migrationBuilder.DropTable(
                name: "AccountRecoveryNotices",
                schema: "platform");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryRequests_CreatedAtUtc_Id",
                schema: "platform",
                table: "AccountRecoveryRequests",
                columns: new[] { "CreatedAtUtc", "Id" },
                filter: "\"AccountNotifiedAtUtc\" IS NULL");
        }
    }
}
