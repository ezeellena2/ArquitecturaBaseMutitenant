using Microsoft.EntityFrameworkCore.Migrations;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

public static class RlsMigrationBuilderExtensions
{
    public static void CreateRlsSupportFunctions(this MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RlsSql.CreateSupportFunctions());

    public static void EnableTenantRls(this MigrationBuilder migrationBuilder, string schema, string table) =>
        migrationBuilder.Sql(RlsSql.EnableTenantRls(schema, table));

    public static void EnablePublicRls(this MigrationBuilder migrationBuilder, string schema, string table) =>
        migrationBuilder.Sql(RlsSql.EnablePublicRls(schema, table));

    public static void EnablePartiesRls(this MigrationBuilder migrationBuilder, string schema, string table) =>
        migrationBuilder.Sql(RlsSql.EnablePartiesRls(schema, table));

    public static void PreventUpdateDelete(this MigrationBuilder migrationBuilder, string schema, string table) =>
        migrationBuilder.Sql(RlsSql.PreventUpdateDelete(schema, table));
}
