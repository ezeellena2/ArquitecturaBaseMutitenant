using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitUserNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "AspNetUsers",
                type: "character varying(201)",
                maxLength: 201,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "identity",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "identity",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // ADR 0046: sólo datos personales vigentes. Una cuenta eliminada conserva su rótulo anónimo.
            migrationBuilder.Sql("""
                WITH names AS (
                    SELECT "Id", NULLIF(btrim(regexp_replace("DisplayName", '[[:space:]]+', ' ', 'g')), '') AS name
                    FROM identity."AspNetUsers" WHERE "Status" <> 'Deleted'
                )
                UPDATE identity."AspNetUsers" AS account
                SET "FirstName" = split_part(names.name, ' ', 1),
                    "LastName" = CASE WHEN strpos(names.name, ' ') > 0
                        THEN NULLIF(substr(names.name, strpos(names.name, ' ') + 1), '') END,
                    "DisplayName" = names.name
                FROM names WHERE account."Id" = names."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(201)",
                oldMaxLength: 201,
                oldNullable: true);
        }
    }
}
