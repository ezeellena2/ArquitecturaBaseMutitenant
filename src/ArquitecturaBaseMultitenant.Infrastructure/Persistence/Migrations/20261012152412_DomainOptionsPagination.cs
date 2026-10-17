using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DomainOptionsPagination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TenantDomains_OrganizationId_Domain_Id",
                schema: "platform",
                table: "TenantDomains",
                columns: new[] { "OrganizationId", "Domain", "Id" });
            migrationBuilder.Sql("""
                CREATE INDEX "IX_TenantDomains_Search_Domain" ON platform."TenantDomains"
                    USING gin (public.f_unaccent(lower("Domain")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX platform.\"IX_TenantDomains_Search_Domain\";");
            migrationBuilder.DropIndex(
                name: "IX_TenantDomains_OrganizationId_Domain_Id",
                schema: "platform",
                table: "TenantDomains");
        }
    }
}
