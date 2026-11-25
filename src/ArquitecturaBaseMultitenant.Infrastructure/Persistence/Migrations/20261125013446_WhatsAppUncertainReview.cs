using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0066 en WhatsApp (B7): cada mensaje de una conversación suma quién lo revisó y cuándo, si quedó «Sin confirmar»; el
    /// estado no se guarda, sale de la cola al leer. Los motivos guardados de lo que no salió pasan a los códigos nuevos
    /// (<c>WhatsAppSendFailures</c>), en la cola, en los mensajes de las conversaciones y en los destinatarios de las difusiones;
    /// las tablas de la organización se recorren con su alcance, porque su RLS es forzada. La 0060 pide leer un valor nuevo un
    /// despliegue antes de escribirlo; esta migración lo escribe en el mismo (excepción anotada de esta etapa: todavía no hay
    /// despliegue, 123). El <c>Down</c> devuelve los cuatro códigos que cambiaron de nombre; los demás la versión anterior los
    /// muestra como un motivo que no conoce.
    /// </summary>
    public partial class WhatsAppUncertainReview : Migration
    {
        private const string Renamed = """
            ('WhatsApp.Send.RecipientNotAllowed', 'WhatsApp.Send.NotAllowed'),
            ('WhatsApp.Send.WindowExpired', 'WhatsApp.Send.WindowClosed'),
            ('WhatsApp.Send.Credentials', 'WhatsApp.Send.TokenInvalid'),
            ('WhatsApp.Send.Permissions', 'WhatsApp.Send.PermissionMissing')
            """;

        private const string Retired = """
            ('WhatsApp.Send.Network', 'WhatsApp.Send.Unavailable'),
            ('WhatsApp.Send.Timeout', 'WhatsApp.Send.Unavailable'),
            ('WhatsApp.Send.InvalidResponse', 'WhatsApp.Send.Rejected'),
            ('WhatsApp.Send.ChannelUnavailable', 'WhatsApp.Send.ReadOnly')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                type: "character varying(201)",
                maxLength: 201,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                type: "character varying(201)",
                maxLength: 201,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(Recode(Renamed + "," + Retired, reverse: false));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Recode(Renamed, reverse: true));

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                schema: "tenant",
                table: "WhatsAppChatMessages");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                schema: "tenant",
                table: "WhatsAppChatMessages");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                schema: "tenant",
                table: "WhatsAppChatMessages");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages");
        }

        /// <summary>Pasa cada código guardado a su par, en la cola y, organización por organización, en sus tablas.</summary>
        private static string Recode(string pairs, bool reverse)
        {
            var (from, to) = reverse ? ("map.next", "map.previous") : ("map.previous", "map.next");
            return $"""
                UPDATE platform."OutboxMessages" AS message SET "LastErrorCode" = {to}
                FROM (VALUES {pairs}) AS map(previous, next)
                WHERE message."Channel" = 'whatsapp' AND message."LastErrorCode" = {from};
                UPDATE platform."PlatformWhatsAppChatMessages" AS message SET "FailureReason" = {to}
                FROM (VALUES {pairs}) AS map(previous, next)
                WHERE message."FailureReason" = {from};
                DO $recode$
                DECLARE current_tenant_id uuid;
                DECLARE previous_scope text := current_setting('app.tenant_id', true);
                BEGIN
                    -- mt_owner también respeta la RLS forzada: cada actualización usa el alcance de su organización.
                    FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" LOOP
                        PERFORM set_config('app.tenant_id', current_tenant_id::text, true);
                        UPDATE tenant."WhatsAppChatMessages" AS message SET "FailureReason" = {to}
                        FROM (VALUES {pairs}) AS map(previous, next)
                        WHERE message."TenantId" = current_tenant_id AND message."FailureReason" = {from};
                        UPDATE tenant."WhatsAppBroadcastRecipients" AS recipient SET "FailureReason" = {to}
                        FROM (VALUES {pairs}) AS map(previous, next)
                        WHERE recipient."TenantId" = current_tenant_id AND recipient."FailureReason" = {from};
                    END LOOP;
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                END
                $recode$;
                """;
        }
    }
}
