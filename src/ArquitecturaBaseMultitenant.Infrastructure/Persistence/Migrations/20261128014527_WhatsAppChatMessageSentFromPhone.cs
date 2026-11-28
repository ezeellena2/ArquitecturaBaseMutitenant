using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppChatMessageSentFromPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SentFromPhone",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SentFromPhone",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un eco del celular ya guardado volvería como respuesta automática del bot al subir otra vez: se niega antes que
            // perder la marca. WhatsAppChatMessages fuerza la RLS también para su dueño, que corre la migración sin organización:
            // la guarda mira entre NO FORCE y FORCE, en la transacción de la migración. El SQL va en una sola línea.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."WhatsAppChatMessages" IN ACCESS EXCLUSIVE MODE; LOCK TABLE platform."PlatformWhatsAppChatMessages" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."WhatsAppChatMessages" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."WhatsAppChatMessages" WHERE "SentFromPhone") OR EXISTS (SELECT 1 FROM platform."PlatformWhatsAppChatMessages" WHERE "SentFromPhone") THEN RAISE EXCEPTION 'A message sent from the phone cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."WhatsAppChatMessages" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "SentFromPhone",
                schema: "tenant",
                table: "WhatsAppChatMessages");

            migrationBuilder.DropColumn(
                name: "SentFromPhone",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages");
        }
    }
}
