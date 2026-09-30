using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "identity",
                table: "LoginMethods",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity",
                table: "AspNetUsers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "ReauthTickets",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SourceMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReauthTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReauthTickets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginMethods_UserId_Primary",
                schema: "identity",
                table: "LoginMethods",
                column: "UserId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ReauthTickets_TokenHash",
                schema: "identity",
                table: "ReauthTickets",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReauthTickets_UserId_ExpiresAtUtc",
                schema: "identity",
                table: "ReauthTickets",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."ReauthTickets" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReauthTickets",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_LoginMethods_UserId_Primary",
                schema: "identity",
                table: "LoginMethods");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "identity",
                table: "LoginMethods");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
