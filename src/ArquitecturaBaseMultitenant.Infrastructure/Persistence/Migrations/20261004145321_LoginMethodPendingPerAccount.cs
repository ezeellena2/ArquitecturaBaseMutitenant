using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoginMethodPendingPerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoginMethods_Type_Value",
                schema: "identity",
                table: "LoginMethods");

            migrationBuilder.CreateIndex(
                name: "IX_LoginMethods_Type_Value",
                schema: "identity",
                table: "LoginMethods",
                columns: new[] { "Type", "Value" },
                unique: true,
                filter: "\"VerifiedAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoginMethods_UserId_Type_Value",
                schema: "identity",
                table: "LoginMethods",
                columns: new[] { "UserId", "Type", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoginMethods_Type_Value",
                schema: "identity",
                table: "LoginMethods");

            migrationBuilder.DropIndex(
                name: "IX_LoginMethods_UserId_Type_Value",
                schema: "identity",
                table: "LoginMethods");

            migrationBuilder.CreateIndex(
                name: "IX_LoginMethods_Type_Value",
                schema: "identity",
                table: "LoginMethods",
                columns: new[] { "Type", "Value" },
                unique: true);
        }
    }
}
