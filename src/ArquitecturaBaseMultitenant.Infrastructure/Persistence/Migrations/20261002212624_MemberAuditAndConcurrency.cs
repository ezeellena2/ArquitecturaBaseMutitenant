using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MemberAuditAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                schema: "tenant",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE tenant.\"Members\" SET \"CreatedAtUtc\" = COALESCE(\"JoinedAtUtc\", CURRENT_TIMESTAMP);");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                schema: "tenant",
                table: "Members",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "tenant",
                table: "Members",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                schema: "tenant",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "tenant",
                table: "Members",
                type: "uuid",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "tenant",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "tenant",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                schema: "tenant",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "tenant",
                table: "Members");

        }
    }
}
