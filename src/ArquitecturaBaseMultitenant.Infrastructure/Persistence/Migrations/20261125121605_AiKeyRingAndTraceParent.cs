using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 0064 §11 (B15): <c>platform.AiKeyRingControls</c>, el control de cada clave del anillo de Data Protection, global y sin
    /// borrado (mt_app solo lee y agrega: nunca se borra ni se cambia un control para que pase otro anillo), y la traza de quien
    /// pidió el turno en <c>platform.AssistantTurnWorks</c>. Aditiva; se escribe en el mismo despliegue que la lee, como excepción
    /// anotada de la 0060 en esta etapa (todavía no hay despliegue, 123): una versión anterior no escribe controles ni la traza, y
    /// las dos columnas nuevas admiten su ausencia. El Down se niega si hay un control guardado: borrarlo dejaría que la próxima
    /// copia, con cualquier anillo, escriba los que mandan. La traza no frena el Down: es diagnóstico, un turno sin ella se arma
    /// igual y solo pierde el padre de su actividad.
    /// </summary>
    public partial class AiKeyRingAndTraceParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "platform",
                table: "AssistantTurnWorks",
                type: "character varying(55)",
                maxLength: 55,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiKeyRingControls",
                schema: "platform",
                columns: table => new
                {
                    KeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Control = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiKeyRingControls", x => x.KeyId);
                });

            migrationBuilder.Sql("GRANT SELECT, INSERT ON TABLE platform.\"AiKeyRingControls\" TO mt_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 0064 §11: nunca se borra un control para que pase otro anillo. Se niega antes de cualquier DDL; la tabla es de
            // platform, sin RLS, y el lock va en el orden de los Drop. El SQL va en una línea: no lleva \r aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE platform."AiKeyRingControls" IN ACCESS EXCLUSIVE MODE; DO $$ BEGIN IF EXISTS (SELECT 1 FROM platform."AiKeyRingControls") THEN RAISE EXCEPTION 'A key ring control cannot be dropped: another ring would pass as the installation one.'; END IF; END $$;
                """);

            migrationBuilder.DropTable(
                name: "AiKeyRingControls",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "platform",
                table: "AssistantTurnWorks");
        }
    }
}
