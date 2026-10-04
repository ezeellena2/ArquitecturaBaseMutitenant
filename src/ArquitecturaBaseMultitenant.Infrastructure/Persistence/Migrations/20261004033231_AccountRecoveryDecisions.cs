using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountRecoveryDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAtUtc",
                schema: "platform",
                table: "AccountRecoveryRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecidedByOperatorId",
                schema: "platform",
                table: "AccountRecoveryRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                schema: "platform",
                table: "AccountRecoveryRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                schema: "platform",
                table: "AccountRecoveryRequests");

            migrationBuilder.DropColumn(
                name: "DecidedByOperatorId",
                schema: "platform",
                table: "AccountRecoveryRequests");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                schema: "platform",
                table: "AccountRecoveryRequests");
        }
    }
}
