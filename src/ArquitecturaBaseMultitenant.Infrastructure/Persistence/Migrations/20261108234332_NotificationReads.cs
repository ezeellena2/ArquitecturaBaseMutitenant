using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationReads",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_ReadAtUtc",
                schema: "identity",
                table: "NotificationReads",
                column: "ReadAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_UserId_NotificationId",
                schema: "identity",
                table: "NotificationReads",
                columns: new[] { "UserId", "NotificationId" },
                unique: true);

            // La campana marca leído, la limpieza borra los de más de 90 días y la baja los de la cuenta: nunca se editan.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, DELETE ON TABLE identity."NotificationReads" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationReads",
                schema: "identity");
        }
    }
}
