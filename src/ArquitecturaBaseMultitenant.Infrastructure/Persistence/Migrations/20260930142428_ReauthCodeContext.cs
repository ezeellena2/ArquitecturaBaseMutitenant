using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReauthCodeContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReauthAction",
                schema: "identity",
                table: "LoginCodes",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceMethodId",
                schema: "identity",
                table: "LoginCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetMethodId",
                schema: "identity",
                table: "LoginCodes",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReauthAction",
                schema: "identity",
                table: "LoginCodes");

            migrationBuilder.DropColumn(
                name: "SourceMethodId",
                schema: "identity",
                table: "LoginCodes");

            migrationBuilder.DropColumn(
                name: "TargetMethodId",
                schema: "identity",
                table: "LoginCodes");
        }
    }
}
