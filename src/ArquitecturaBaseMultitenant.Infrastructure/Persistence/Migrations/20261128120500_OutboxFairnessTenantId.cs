using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// KAN-113: suma <c>TenantId</c> (nullable) y su índice parcial para repartir la cola con equidad entre organizaciones
    /// dentro de cada prioridad, sin afectar a versiones anteriores que insertan sin esa columna.
    /// </summary>
    public partial class OutboxFairnessTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "platform",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Fairness",
                schema: "platform",
                table: "OutboxMessages",
                columns: new[] { "Priority", "TenantId", "NextAttemptAtUtc", "Id" },
                filter: "\"Status\" IN ('Pending', 'Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Fairness",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "platform",
                table: "OutboxMessages");
        }
    }
}
