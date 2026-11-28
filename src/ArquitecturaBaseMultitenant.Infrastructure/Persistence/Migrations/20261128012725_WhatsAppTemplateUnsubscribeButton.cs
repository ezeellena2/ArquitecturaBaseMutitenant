using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppTemplateUnsubscribeButton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasUnsubscribeButton",
                schema: "tenant",
                table: "WhatsAppTemplates",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasUnsubscribeButton",
                schema: "platform",
                table: "PlatformWhatsAppTemplates",
                type: "boolean",
                nullable: true);

            // Las filas ya guardadas conservan si se crearon con el botón «No recibir más» (Marketing). La columna admite null y
            // no tiene valor por defecto: lo que inserte la versión anterior mientras conviven queda en null y se lee por su
            // categoría, como lo leía esa versión (ADR 0060). WhatsAppTemplates fuerza la RLS también para su dueño, que corre la
            // migración sin organización: sin NO FORCE el UPDATE no vería ninguna fila. Todo corre en la transacción de la
            // migración, y al terminar la RLS queda forzada.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."WhatsAppTemplates", platform."PlatformWhatsAppTemplates" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."WhatsAppTemplates" NO FORCE ROW LEVEL SECURITY; UPDATE tenant."WhatsAppTemplates" SET "HasUnsubscribeButton" = ("Category" = 'Marketing'); UPDATE platform."PlatformWhatsAppTemplates" SET "HasUnsubscribeButton" = ("Category" = 'Marketing'); ALTER TABLE tenant."WhatsAppTemplates" FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasUnsubscribeButton",
                schema: "tenant",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "HasUnsubscribeButton",
                schema: "platform",
                table: "PlatformWhatsAppTemplates");
        }
    }
}
