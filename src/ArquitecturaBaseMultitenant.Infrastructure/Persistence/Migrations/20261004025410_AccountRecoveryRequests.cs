using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountRecoveryRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NonceHash",
                schema: "identity",
                table: "LoginCodes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountRecoveryRequests",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousMethodType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PreviousMethodValue = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NewMethodType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NewMethodValue = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRecoveryRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryRequests_LoginCodeId",
                schema: "platform",
                table: "AccountRecoveryRequests",
                column: "LoginCodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryRequests_Status_CreatedAtUtc_Id",
                schema: "platform",
                table: "AccountRecoveryRequests",
                columns: new[] { "Status", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryRequests_UserId",
                schema: "platform",
                table: "AccountRecoveryRequests",
                column: "UserId");

            // Tabla global: la aplicación registra y resuelve pedidos; la baja de una cuenta los cierra.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON platform."AccountRecoveryRequests" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRecoveryRequests",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "NonceHash",
                schema: "identity",
                table: "LoginCodes");
        }
    }
}
