using Microsoft.EntityFrameworkCore.Migrations;

using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityRlsAndGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "Members");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "TenantSettings");

            // Initial grants cover only tables present in the initial migration.
            // New tables get the minimum runtime permissions required by the use cases.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE ON TABLE identity."AspNetUsers" TO mt_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."AspNetUserClaims", identity."AspNetUserLogins", identity."AspNetUserTokens" TO mt_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."LoginMethods", identity."LoginCodes" TO mt_app;
                GRANT SELECT, INSERT ON TABLE identity."LoginAudits" TO mt_app;
                GRANT SELECT, INSERT, UPDATE ON TABLE identity."LegalAcceptances" TO mt_app;
                GRANT SELECT, INSERT, UPDATE ON TABLE tenant."Members" TO mt_app;
                GRANT SELECT, INSERT, UPDATE ON TABLE tenant."TenantSettings" TO mt_app;
                GRANT SELECT, INSERT, UPDATE ON TABLE platform."Tenants", platform."PlatformSettings", platform."OutboxMessages" TO mt_app;
                GRANT SELECT, INSERT ON TABLE platform."SecurityEvents", platform."LegalDocuments", platform."LegalDocumentContents" TO mt_app;
                GRANT USAGE, SELECT ON SEQUENCE identity."AspNetUserClaims_Id_seq" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                REVOKE ALL PRIVILEGES ON TABLE identity."AspNetUsers", identity."AspNetUserClaims", identity."AspNetUserLogins", identity."AspNetUserTokens", identity."LoginMethods", identity."LoginCodes", identity."LoginAudits", identity."LegalAcceptances", tenant."Members", tenant."TenantSettings", platform."Tenants", platform."PlatformSettings", platform."OutboxMessages", platform."SecurityEvents", platform."LegalDocuments", platform."LegalDocumentContents" FROM mt_app;
                REVOKE ALL PRIVILEGES ON SEQUENCE identity."AspNetUserClaims_Id_seq" FROM mt_app;
                DROP TRIGGER prevent_tenant_change ON tenant."Members";
                DROP POLICY tenant_scope ON tenant."Members";
                ALTER TABLE tenant."Members" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant."Members" NO FORCE ROW LEVEL SECURITY;
                DROP TRIGGER prevent_tenant_change ON tenant."TenantSettings";
                DROP POLICY tenant_scope ON tenant."TenantSettings";
                ALTER TABLE tenant."TenantSettings" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant."TenantSettings" NO FORCE ROW LEVEL SECURITY;
                """);
        }
    }
}
