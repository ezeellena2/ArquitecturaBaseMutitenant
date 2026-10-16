using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// E9 (B5): la política del asistente pierde el tope diario, la retención y el presupuesto mensual, que nadie lee desde
    /// que Plataforma los fija para todas las organizaciones. El Down vuelve a crear las columnas vacías: los valores no vuelven.
    /// </summary>
    public partial class AssistantPolicyDeadColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyMessageLimit",
                schema: "platform",
                table: "AssistantOrganizationPolicies");

            migrationBuilder.DropColumn(
                name: "MonthlyBudgetAmount",
                schema: "platform",
                table: "AssistantOrganizationPolicies");

            migrationBuilder.DropColumn(
                name: "MonthlyBudgetCurrency",
                schema: "platform",
                table: "AssistantOrganizationPolicies");

            migrationBuilder.DropColumn(
                name: "RetentionDays",
                schema: "platform",
                table: "AssistantOrganizationPolicies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyMessageLimit",
                schema: "platform",
                table: "AssistantOrganizationPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyBudgetAmount",
                schema: "platform",
                table: "AssistantOrganizationPolicies",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonthlyBudgetCurrency",
                schema: "platform",
                table: "AssistantOrganizationPolicies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                schema: "platform",
                table: "AssistantOrganizationPolicies",
                type: "integer",
                nullable: true);
        }
    }
}
