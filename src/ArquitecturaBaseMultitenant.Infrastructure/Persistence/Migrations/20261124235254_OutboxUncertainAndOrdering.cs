using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0066 (N5, N8 y O3): la cola suma «Sin confirmar» (<c>Status</c> = <c>Uncertain</c>, texto como los demás), el HMAC
    /// del destino que ordena, la prioridad (0 contesta, 1 avisa; 1 por defecto, también para lo que inserte la versión
    /// anterior) y el plazo después del cual no se manda, con sus índices parciales sobre lo que todavía puede salir. Lo que
    /// esperaba un reintento pasa a <c>Pending</c>: desde acá <c>Failed</c> es solo lo que no salió, y la versión anterior lo
    /// reclama igual. La 0060 pide leer un valor nuevo un despliegue antes de escribirlo; esta migración lo escribe en el
    /// mismo (excepción anotada de esta etapa: todavía no hay despliegue, 123). Al volver a la imagen anterior, una fila
    /// <c>Uncertain</c> la haría fallar al leerla: el <c>Down</c> se niega si hay alguna, antes de cualquier DDL.
    /// </summary>
    public partial class OutboxUncertainAndOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DiscardAfterUtc",
                schema: "platform",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderingKey",
                schema: "platform",
                table: "OutboxMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Priority",
                schema: "platform",
                table: "OutboxMessages",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Due",
                schema: "platform",
                table: "OutboxMessages",
                columns: new[] { "Priority", "NextAttemptAtUtc", "Id" },
                filter: "\"Status\" IN ('Pending', 'Failed')");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Ordering",
                schema: "platform",
                table: "OutboxMessages",
                columns: new[] { "OrderingKey", "CreatedAtUtc", "Id" },
                filter: "\"Status\" IN ('Pending', 'Failed')");

            migrationBuilder.Sql("""
                UPDATE platform."OutboxMessages" SET "Status" = 'Pending' WHERE "Status" = 'Failed' AND "NextAttemptAtUtc" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Una fila «Sin confirmar» no la puede leer la versión anterior, y convertirla diría algo que no se sabe: se niega
            // antes de borrar nada. El SQL va en una línea: no lleva \r aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE platform."OutboxMessages" IN ACCESS EXCLUSIVE MODE; DO $$ BEGIN IF EXISTS (SELECT 1 FROM platform."OutboxMessages" WHERE "Status" = 'Uncertain') THEN RAISE EXCEPTION 'An unconfirmed outbox message cannot be represented by the previous schema.'; END IF; END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Due",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Ordering",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DiscardAfterUtc",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "OrderingKey",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "platform",
                table: "OutboxMessages");
        }
    }
}
