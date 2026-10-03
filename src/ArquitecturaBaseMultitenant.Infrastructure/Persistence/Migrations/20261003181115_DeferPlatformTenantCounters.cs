using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeferPlatformTenantCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los deltas comparten la transacción, pero no adelantan xmin antes del UPDATE de estado de EF.
            migrationBuilder.Sql("""
                DROP TRIGGER sync_platform_company_count ON tenant."Companies";
                DROP TRIGGER sync_platform_member_count ON tenant."Members";
                CREATE CONSTRAINT TRIGGER sync_platform_company_count AFTER INSERT OR DELETE ON tenant."Companies"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION platform.sync_platform_company_count();
                CREATE CONSTRAINT TRIGGER sync_platform_member_count AFTER INSERT OR UPDATE OF "Status" OR DELETE ON tenant."Members"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION platform.sync_platform_member_count();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER sync_platform_company_count ON tenant."Companies";
                DROP TRIGGER sync_platform_member_count ON tenant."Members";
                CREATE TRIGGER sync_platform_company_count AFTER INSERT OR DELETE ON tenant."Companies"
                    FOR EACH ROW EXECUTE FUNCTION platform.sync_platform_company_count();
                CREATE TRIGGER sync_platform_member_count AFTER INSERT OR UPDATE OF "Status" OR DELETE ON tenant."Members"
                    FOR EACH ROW EXECUTE FUNCTION platform.sync_platform_member_count();
                """);
        }
    }
}
