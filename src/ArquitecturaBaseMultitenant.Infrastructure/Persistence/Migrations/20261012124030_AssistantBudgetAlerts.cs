using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantBudgetAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantBudgetAlerts",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthId = table.Column<Guid>(type: "uuid", nullable: false),
                    Funding = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    UsedAmount = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    UsedCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    BudgetCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    EnqueuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantBudgetAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantBudgetAlerts_Currencies_BudgetCurrency",
                        column: x => x.BudgetCurrency,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantBudgetAlerts_Currencies_UsedCurrency",
                        column: x => x.UsedCurrency,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.\"AssistantBudgetAlerts\" TO mt_app;");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBudgetAlerts_BudgetCurrency",
                schema: "platform",
                table: "AssistantBudgetAlerts",
                column: "BudgetCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBudgetAlerts_MonthId_Funding_Threshold",
                schema: "platform",
                table: "AssistantBudgetAlerts",
                columns: new[] { "MonthId", "Funding", "Threshold" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBudgetAlerts_UsedCurrency",
                schema: "platform",
                table: "AssistantBudgetAlerts",
                column: "UsedCurrency");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantBudgetAlerts",
                schema: "platform");
        }
    }
}
