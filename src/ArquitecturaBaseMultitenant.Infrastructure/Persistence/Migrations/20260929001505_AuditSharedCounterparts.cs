using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditSharedCounterparts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE POLICY audit_counterpart_insert ON tenant."AuditEntries"
                AS PERMISSIVE FOR INSERT TO mt_app
                WITH CHECK (
                    "TenantId" = ANY(
                        string_to_array(
                            nullif(current_setting('app.audit_counterpart_tenant_ids', true), ''),
                            ','
                        )::uuid[]
                    )
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY audit_counterpart_insert ON tenant.\"AuditEntries\";");
        }
    }
}
