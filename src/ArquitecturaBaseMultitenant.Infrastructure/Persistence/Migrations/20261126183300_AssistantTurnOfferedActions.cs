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
            migrationBuilder.DropColumn(
                name: "OfferedActions",
                schema: "tenant",
                table: "AssistantTurns");
        }
    }
}
