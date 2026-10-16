using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSecondFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformAuthenticators",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedSecret = table.Column<byte[]>(type: "bytea", nullable: false),
                    LastAcceptedTimeStep = table.Column<long>(type: "bigint", nullable: false),
                    ConfiguredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAuthenticators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformAuthenticators_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformFactorChallenges",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BindingHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReturnUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Step = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProtectedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                    AcceptedTimeStep = table.Column<long>(type: "bigint", nullable: true),
                    ProtectedRecoveryCodes = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformFactorChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformFactorChallenges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformFactorOperations",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlatformSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BodyHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Route = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ReturnUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProtectedSessionId = table.Column<byte[]>(type: "bytea", nullable: true),
                    ShowsRecoveryCodes = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecentUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformFactorOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformFactorOperations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRecoveryCodes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformRecoveryCodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionIdHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Method = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecentProofAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecentProofUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuthenticators_UserId",
                schema: "identity",
                table: "PlatformAuthenticators",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFactorChallenges_BindingHash",
                schema: "identity",
                table: "PlatformFactorChallenges",
                column: "BindingHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFactorChallenges_UserId_ExpiresAtUtc",
                schema: "identity",
                table: "PlatformFactorChallenges",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFactorOperations_IdempotencyKey",
                schema: "identity",
                table: "PlatformFactorOperations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFactorOperations_UserId_CreatedAtUtc",
                schema: "identity",
                table: "PlatformFactorOperations",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRecoveryCodes_UserId_CodeHash",
                schema: "identity",
                table: "PlatformRecoveryCodes",
                columns: new[] { "UserId", "CodeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSessions_SessionIdHash",
                schema: "identity",
                table: "PlatformSessions",
                column: "SessionIdHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSessions_UserId_StartedAtUtc",
                schema: "identity",
                table: "PlatformSessions",
                columns: new[] { "UserId", "StartedAtUtc" });

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."PlatformAuthenticators", identity."PlatformRecoveryCodes" TO mt_app;
                GRANT SELECT, INSERT, UPDATE ON TABLE identity."PlatformFactorChallenges", identity."PlatformSessions" TO mt_app;
                GRANT SELECT, INSERT ON TABLE identity."PlatformFactorOperations" TO mt_app;
                """);

            // E9: ninguna sesión de Plataforma anterior al segundo factor sigue valiendo, tampoco la del operador del
            // seed. Se revocan sus autorizaciones y tokens, y el sello nuevo cierra sus cookies: el próximo ingreso de
            // cada operador pasa por el segundo factor y, sin autenticador, por el alta.
            migrationBuilder.Sql("""
                UPDATE platform."OpenIddictTokens" SET "Status" = 'revoked'
                WHERE "AuthorizationId" IN (
                    SELECT "Id" FROM platform."OpenIddictAuthorizations"
                    WHERE "Properties" IS NOT NULL AND "Properties"::jsonb ->> 'access' = 'platform');
                UPDATE platform."OpenIddictAuthorizations" SET "Status" = 'revoked'
                WHERE "Properties" IS NOT NULL AND "Properties"::jsonb ->> 'access' = 'platform';
                UPDATE identity."AspNetUsers"
                SET "SecurityStamp" = upper(replace(gen_random_uuid()::text, '-', '')),
                    "ConcurrencyStamp" = gen_random_uuid()::text
                WHERE "IsPlatformOperator";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAuthenticators",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PlatformFactorChallenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PlatformFactorOperations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PlatformRecoveryCodes",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PlatformSessions",
                schema: "identity");
        }
    }
}
