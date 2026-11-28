using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppMessageFailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un fallo de una organización que la cola todavía no anotó perdería su motivo, y la versión anterior no lo toma: se
            // niega antes que borrarlo. Uno ya anotado no pierde nada, porque el motivo quedó en el chat o en la difusión.
            migrationBuilder.Sql("""
                LOCK TABLE identity."WhatsAppMessages" IN ACCESS EXCLUSIVE MODE; DO $$ BEGIN IF EXISTS (SELECT 1 FROM identity."WhatsAppMessages" WHERE "EventType" = 'delivery_failure' AND "ProcessedAtUtc" IS NULL) THEN RAISE EXCEPTION 'A WhatsApp delivery failure that was not recorded yet cannot be represented by the previous schema.'; END IF; END $$;
                """);

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "identity",
                table: "WhatsAppMessages");
        }
    }
}
