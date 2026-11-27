using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// El tope de la instalación de la barrera de archivos de los catálogos (C11): <c>mt_app</c> ve solo las barreras de su
    /// organización, y la instalación se cuenta con <c>tenant.count_form_work_leases</c>, de <c>mt_owner</c>, que lee todas por una
    /// política solo de lectura para ese rol y devuelve solo cuántas siguen vigentes. Sin columnas ni datos: dos versiones conviven
    /// (ADR 0060) porque la anterior no la llama.
    /// </summary>
    public partial class FormWorkLeaseCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE POLICY form_work_lease_capacity ON tenant."FormWorkLeases" FOR SELECT TO mt_owner USING (true);
                CREATE FUNCTION tenant.count_form_work_leases(at_utc timestamp with time zone) RETURNS integer
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog AS $function$
                    SELECT count(*)::integer FROM tenant."FormWorkLeases" WHERE "ExpiresAtUtc" > at_utc
                $function$;
                REVOKE ALL ON FUNCTION tenant.count_form_work_leases(timestamp with time zone) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION tenant.count_form_work_leases(timestamp with time zone) TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION tenant.count_form_work_leases(timestamp with time zone);
                DROP POLICY form_work_lease_capacity ON tenant."FormWorkLeases";
                """);
        }
    }
}
