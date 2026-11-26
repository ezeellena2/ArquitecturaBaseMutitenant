using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Acciones del asistente, B4 (ADR 0068 §6): <c>tenant.AssistantTurns</c> suma <c>OfferedActions</c>, la lista sellada de
    /// las acciones que ofreció el turno, nula en los turnos sin acciones y en los de antes. Aditiva: la versión anterior no la
    /// lee ni la escribe, y un turno con acciones se sella con <c>EngineVersion</c> 2, que esa versión no toma (0064 §6).
    /// </summary>
    public partial class AssistantTurnOfferedActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfferedActions",
                schema: "tenant",
                table: "AssistantTurns",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un turno que ofreció acciones perdería la lista sellada, y si se recupera, ya no sabría qué podía proponer: se
            // niega antes que borrarla, antes de cualquier DDL (guía de migración, paso 4). AssistantTurns fuerza la RLS también
            // para su dueño, que corre la migración sin organización: sin NO FORCE la guarda no vería ningún turno. Va en la
            // transacción de la migración: si se niega, la RLS sigue forzada. El SQL va en una línea: no lleva \r aunque el
            // archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."AssistantTurns" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."AssistantTurns" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."AssistantTurns" WHERE "OfferedActions" IS NOT NULL) THEN RAISE EXCEPTION 'A turn that offered assistant actions cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."AssistantTurns" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "OfferedActions",
                schema: "tenant",
                table: "AssistantTurns");
        }
    }
}
