using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// B6 (ADR 0066 §4): cada invitación de la organización y la de un operador (en su cuenta) guardan el mensaje de la cola
    /// que las lleva, y su estado de entrega sale de la cola al leer. Columnas nulas, sin clave foránea: la cola purga sus
    /// filas, y sin fila (o sin id, como las anteriores y las que emita la versión anterior) la invitación se lee enviada.
    /// La 0060 pide leer un valor nuevo un despliegue antes de escribirlo; esta migración lo escribe en el mismo (excepción
    /// anotada de esta etapa: todavía no hay despliegue, 123). La versión anterior no lee las columnas.
    /// </summary>
    public partial class InvitationDeliveryOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OutboxMessageId",
                schema: "tenant",
                table: "Invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperatorInvitationOutboxMessageId",
                schema: "identity",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin el id, una invitación pendiente cuyo mensaje no salió (en la cola, «Sin confirmar», fallido o cancelado) se
            // leería enviada al volver a subir, y nadie la reenviaría: se niega antes que borrarlo. Una enviada, purgada o
            // cerrada no pierde nada: ya se lee enviada o no muestra entrega. La de un operador cuenta mientras su cuenta no
            // esté eliminada (la revocada la elimina). Invitations fuerza la RLS también para su dueño, que corre la migración
            // sin organización: sin NO FORCE la guarda no vería ninguna invitación. Va en la transacción de la migración: si
            // se niega, la RLS sigue forzada. Los locks van en el orden de los DropColumn. El SQL va en una línea: no lleva \r
            // aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."Invitations" IN ACCESS EXCLUSIVE MODE; LOCK TABLE identity."AspNetUsers" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."Invitations" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."Invitations" AS i JOIN platform."OutboxMessages" AS o ON o."Id" = i."OutboxMessageId" WHERE i."Status" = 'Pending' AND o."Status" <> 'Sent') OR EXISTS (SELECT 1 FROM identity."AspNetUsers" AS u JOIN platform."OutboxMessages" AS o ON o."Id" = u."OperatorInvitationOutboxMessageId" WHERE u."OperatorInvitationExpiresAtUtc" IS NOT NULL AND u."Status" <> 'Deleted' AND o."Status" <> 'Sent') THEN RAISE EXCEPTION 'A pending invitation whose message did not go out cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."Invitations" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "OutboxMessageId",
                schema: "tenant",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "OperatorInvitationOutboxMessageId",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
