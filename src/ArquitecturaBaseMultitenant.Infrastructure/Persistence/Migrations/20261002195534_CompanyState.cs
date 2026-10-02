using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" RENAME COLUMN \"IsActive\" TO \"Status\";");
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" ALTER COLUMN \"Status\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" ALTER COLUMN \"Status\" TYPE character varying(20) USING CASE WHEN \"Status\" THEN 'Active' ELSE 'Inactive' END;");
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" ALTER COLUMN \"Status\" SET DEFAULT 'Active';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" ALTER COLUMN \"Status\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" ALTER COLUMN \"Status\" TYPE boolean USING \"Status\" = 'Active';");
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" RENAME COLUMN \"Status\" TO \"IsActive\";");
            migrationBuilder.Sql("ALTER TABLE tenant.\"Companies\" ALTER COLUMN \"IsActive\" SET DEFAULT TRUE;");
        }
    }
}
