using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0064 §5, §6 y la 271 (B14). El turno suma la versión del motor que lo selló (1 para los que ya están: los selló esta);
    /// el worker toma solo los que conoce. El cupo diario con la clave de la plataforma suma la organización (nula fuera de una)
    /// y pasa a ser único por persona, organización y día, con las nulas como iguales; las filas de antes quedan como el cupo de
    /// fuera de una organización de su día UTC y dejan de contar al día siguiente, salvo lo abierto de un turno de empresa, que
    /// pasa a la fila de su organización. El Down se niega si hay un cupo de una organización o un turno de otro motor. Van los índices parciales de lo pendiente: los
    /// turnos abiertos y los adjuntos sin confirmar. Se escribe en el mismo despliegue que lo lee, como excepción anotada de la
    /// 0060 en esta etapa (todavía no hay despliegue, 123): una versión anterior no conoce la organización del cupo.
    /// </summary>
    public partial class AssistantEngineVersionAndDailyQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssistantPlatformKeyDailyQuotas_UserId_DayUtc",
                schema: "platform",
                table: "AssistantPlatformKeyDailyQuotas");

            migrationBuilder.AddColumn<int>(
                name: "EngineVersion",
                schema: "platform",
                table: "AssistantTurnWorks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "platform",
                table: "AssistantPlatformKeyDailyQuotas",
                type: "uuid",
                nullable: true);

            // Lo abierto de un turno de empresa (reservado o con un paso ya consumido) pasa de la fila de antes, sin organización,
            // a la de su organización y su día: esta versión lo cierra ahí, y sin la fila no termina ni devuelve el cupo. Lo cerrado
            // se queda en la de antes. Antes de esta migración no hay filas con organización: las de su organización son estas. La
            // tabla del cupo ya está tomada por el DropIndex de arriba, en la misma transacción. El SQL va en una línea, sin \r.
            migrationBuilder.Sql("""
                INSERT INTO platform."AssistantPlatformKeyDailyQuotas" ("Id", "UserId", "OrganizationId", "DayUtc", "ReservedQueries", "ConsumedQueries") SELECT gen_random_uuid(), o."UserId", o."ScopeTenantId", o."DayUtc", count(*) FILTER (WHERE o."State" = 'Reserved'), count(*) FILTER (WHERE o."State" = 'Invoked') FROM platform."AssistantFinancialOperations" AS o WHERE o."Access" = 'Business' AND o."Funding" = 'PlatformKey' AND o."State" IN ('Reserved', 'Invoked') AND o."ScopeTenantId" IS NOT NULL GROUP BY o."UserId", o."ScopeTenantId", o."DayUtc"; UPDATE platform."AssistantPlatformKeyDailyQuotas" AS q SET "ReservedQueries" = q."ReservedQueries" - m."Reserved", "ConsumedQueries" = q."ConsumedQueries" - m."Consumed" FROM (SELECT "UserId", "DayUtc", sum("ReservedQueries")::int AS "Reserved", sum("ConsumedQueries")::int AS "Consumed" FROM platform."AssistantPlatformKeyDailyQuotas" WHERE "OrganizationId" IS NOT NULL GROUP BY "UserId", "DayUtc") AS m WHERE q."OrganizationId" IS NULL AND q."UserId" = m."UserId" AND q."DayUtc" = m."DayUtc";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantAttachments_Unconfirmed",
                schema: "platform",
                table: "PlatformAssistantAttachments",
                columns: new[] { "CreatedAtUtc", "Id" },
                filter: "\"MessageId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurnWorks_Open",
                schema: "platform",
                table: "AssistantTurnWorks",
                columns: new[] { "CreatedAtUtc", "Id" },
                filter: "\"Status\" = 'Pending' OR \"Status\" = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantPlatformKeyDailyQuotas_UserId_OrganizationId_DayUtc",
                schema: "platform",
                table: "AssistantPlatformKeyDailyQuotas",
                columns: new[] { "UserId", "OrganizationId", "DayUtc" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantAttachments_Unconfirmed",
                schema: "tenant",
                table: "AssistantAttachments",
                columns: new[] { "TenantId", "CreatedAtUtc", "Id" },
                filter: "\"MessageId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin la organización, el cupo de una pasaría a ser el de fuera de una de esa persona y ese día, y dos el mismo día
            // romperían el índice de antes; sin la versión del motor, la versión anterior tomaría como propio el turno de uno más
            // nuevo. Se niega antes que borrarlas, antes de cualquier DDL. Estas tablas de platform no tienen RLS. Los locks van en
            // el orden de los DropColumn. El SQL va en una línea: no lleva \r aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE platform."AssistantTurnWorks" IN ACCESS EXCLUSIVE MODE; LOCK TABLE platform."AssistantPlatformKeyDailyQuotas" IN ACCESS EXCLUSIVE MODE; DO $$ BEGIN IF EXISTS (SELECT 1 FROM platform."AssistantPlatformKeyDailyQuotas" WHERE "OrganizationId" IS NOT NULL) OR EXISTS (SELECT 1 FROM platform."AssistantTurnWorks" WHERE "EngineVersion" <> 1) THEN RAISE EXCEPTION 'A daily quota of an organization or a turn of a newer engine cannot be represented by the previous schema.'; END IF; END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_PlatformAssistantAttachments_Unconfirmed",
                schema: "platform",
                table: "PlatformAssistantAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AssistantTurnWorks_Open",
                schema: "platform",
                table: "AssistantTurnWorks");

            migrationBuilder.DropIndex(
                name: "IX_AssistantPlatformKeyDailyQuotas_UserId_OrganizationId_DayUtc",
                schema: "platform",
                table: "AssistantPlatformKeyDailyQuotas");

            migrationBuilder.DropIndex(
                name: "IX_AssistantAttachments_Unconfirmed",
                schema: "tenant",
                table: "AssistantAttachments");

            migrationBuilder.DropColumn(
                name: "EngineVersion",
                schema: "platform",
                table: "AssistantTurnWorks");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "platform",
                table: "AssistantPlatformKeyDailyQuotas");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantPlatformKeyDailyQuotas_UserId_DayUtc",
                schema: "platform",
                table: "AssistantPlatformKeyDailyQuotas",
                columns: new[] { "UserId", "DayUtc" },
                unique: true);
        }
    }
}
