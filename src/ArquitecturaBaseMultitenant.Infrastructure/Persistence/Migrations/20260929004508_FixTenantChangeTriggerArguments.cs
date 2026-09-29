using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixTenantChangeTriggerArguments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER prevent_tenant_change ON tenant."AuditEntries";
                CREATE TRIGGER prevent_tenant_change BEFORE UPDATE ON tenant."AuditEntries"
                FOR EACH ROW EXECUTE FUNCTION platform.prevent_tenant_change('TenantId');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The tenant-column trigger security fix cannot be rolled back safely.");
        }
    }
}
