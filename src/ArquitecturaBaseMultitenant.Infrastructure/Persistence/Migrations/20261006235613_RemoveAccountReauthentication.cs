using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccountReauthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los códigos de confirmación anteriores ya no tienen ningún propósito válido.
            migrationBuilder.Sql("""DELETE FROM identity."LoginCodes" WHERE "Purpose" = 'Reauthenticate';""");

            migrationBuilder.DropColumn(
                name: "ReauthAction",
                schema: "identity",
                table: "LoginCodes");

            migrationBuilder.DropColumn(
                name: "SourceMethodId",
                schema: "identity",
                table: "LoginCodes");

            migrationBuilder.DropColumn(
                name: "TargetMethodId",
                schema: "identity",
                table: "LoginCodes");

            migrationBuilder.CreateTable(
                name: "AccountDeletionCancelTickets",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletionCancelTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountDeletionCancelTickets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionCancelTickets_TokenHash",
                schema: "identity",
                table: "AccountDeletionCancelTickets",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionCancelTickets_UserId_ExpiresAtUtc",
                schema: "identity",
                table: "AccountDeletionCancelTickets",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            // Cancelar una baja después de probar el ingreso conserva su prueba y su retorno.
            migrationBuilder.Sql("""GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."AccountDeletionCancelTickets" TO mt_app;""");
            migrationBuilder.Sql("""
                INSERT INTO identity."AccountDeletionCancelTickets"
                    ("Id", "UserId", "TokenHash", "IssuedAtUtc", "ExpiresAtUtc", "ConsumedAtUtc", "ReturnUrl")
                SELECT "Id", "UserId", "TokenHash", "IssuedAtUtc", "ExpiresAtUtc", "ConsumedAtUtc", "ReturnUrl"
                FROM identity."ReauthTickets"
                WHERE "Action" = 'CancelDeletion' AND "ReturnUrl" IS NOT NULL;
                """);
            migrationBuilder.DropTable(name: "ReauthTickets", schema: "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReauthAction",
                schema: "identity",
                table: "LoginCodes",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceMethodId",
                schema: "identity",
                table: "LoginCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetMethodId",
                schema: "identity",
                table: "LoginCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReauthTickets",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SourceMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.Sql("""GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."ReauthTickets" TO mt_app;""");
            migrationBuilder.Sql("""
                INSERT INTO identity."ReauthTickets"
                    ("Id", "UserId", "Action", "TokenHash", "IssuedAtUtc", "ExpiresAtUtc", "ConsumedAtUtc", "ReturnUrl")
                SELECT "Id", "UserId", 'CancelDeletion', "TokenHash", "IssuedAtUtc", "ExpiresAtUtc", "ConsumedAtUtc", "ReturnUrl"
                FROM identity."AccountDeletionCancelTickets";
                """);
            migrationBuilder.DropTable(name: "AccountDeletionCancelTickets", schema: "identity");
        }
    }
}
