using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformRateLimits : Migration
    {
        // PlatformRateLimits.Initial (P3), con los nombres del contrato; la convención lo lee sin distinguir mayúsculas.
        private const string InitialRateLimits = """{"ip":{"permitLimit":300,"windowSeconds":60},"publicIp":{"permitLimit":60,"windowSeconds":60},"identity":{"permitLimit":120,"windowSeconds":60},"tenant":{"permitLimit":600,"windowSeconds":60},"loginCode":{"permitLimit":20,"windowSeconds":900},"loginVerify":{"permitLimit":30,"windowSeconds":900},"invitationPreview":{"permitLimit":60,"windowSeconds":900},"invitationAccept":{"permitLimit":10,"windowSeconds":900},"assistantQuickQuery":{"permitLimit":20,"windowSeconds":60},"assistantConversation":{"permitLimit":10,"windowSeconds":60},"assistantNewKey":{"permitLimit":3,"windowSeconds":900},"whatsAppWebhook":{"permitLimit":120,"windowSeconds":60}}""";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // E9: la configuración existente arranca con las cuotas iniciales; después las edita Plataforma con motivo.
            migrationBuilder.AddColumn<string>(
                name: "RateLimits",
                schema: "platform",
                table: "PlatformSettings",
                type: "jsonb",
                nullable: false,
                defaultValue: InitialRateLimits);

            migrationBuilder.Sql("""
                ALTER TABLE platform."PlatformSettings" ALTER COLUMN "RateLimits" DROP DEFAULT;
                """);

            // La configuración cacheada cambió por SQL, sin pasar por el guardado: se avanza su generación (ADR 0052).
            migrationBuilder.Sql("""
                INSERT INTO platform."CacheTagVersions" ("Tag", "Generation") VALUES ('p:settings', 1)
                ON CONFLICT ("Tag") DO UPDATE SET "Generation" = platform."CacheTagVersions"."Generation" + 1;
                """);

            migrationBuilder.CreateTable(
                name: "RateLimitCounters",
                schema: "platform",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateLimitCounters", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RateLimitCounters_ExpiresAtUtc",
                schema: "platform",
                table: "RateLimitCounters",
                column: "ExpiresAtUtc");

            // El limitador cuenta acá mientras Redis no responde; el worker de limpieza borra las ventanas terminadas.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform."RateLimitCounters" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateLimitCounters",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "RateLimits",
                schema: "platform",
                table: "PlatformSettings");
        }
    }
}
