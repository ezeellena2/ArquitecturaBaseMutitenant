using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0063 y decisión 256: los contadores por hora de los topes de correo y del de códigos por WhatsApp desde Mi cuenta.
    /// Global y sin RLS, como identity."WhatsAppCodeQuotas": la clave es un HMAC, nunca una dirección, una cuenta ni una
    /// organización en claro. mt_app los lee, los suma con un upsert y borra los que ya salieron de la ventana (la purga de
    /// IdentityRetentionWorker). Solo agrega una tabla, así que cumple la 0060; la versión que escribe es la misma que la
    /// lee (excepción anotada de esta etapa: todavía no hay despliegue, 123).
    /// </summary>
    public partial class MessageQuotaBuckets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageQuotaBuckets",
                schema: "identity",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HourUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageQuotaBuckets", x => new { x.Scope, x.KeyHash, x.HourUtc });
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageQuotaBuckets_HourUtc",
                schema: "identity",
                table: "MessageQuotaBuckets",
                column: "HourUtc");

            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity.\"MessageQuotaBuckets\" TO mt_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageQuotaBuckets",
                schema: "identity");
        }
    }
}
