using System.Data;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal static class IsolationSchema
{
    public static async Task ApplyAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var openedHere = connection.State == ConnectionState.Closed;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(CreateSql(), connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string CreateSql() => $"""
        CREATE TABLE "{Schemas.Tenant}"."Widgets" (
            "TenantId" uuid NOT NULL,
            "Id" uuid NOT NULL,
            "Name" text NOT NULL,
            "Email" character varying(254),
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "CreatedBy" uuid,
            "ModifiedAtUtc" timestamp with time zone,
            "ModifiedBy" uuid,
            "IsDeleted" boolean NOT NULL,
            "DeletedAtUtc" timestamp with time zone,
            "DeletedBy" uuid,
            CONSTRAINT "PK_Widgets" PRIMARY KEY ("TenantId", "Id")
        );
        CREATE INDEX "IX_Widgets_TenantId_Name_Id" ON "{Schemas.Tenant}"."Widgets" ("TenantId", "Name", "Id");

        CREATE TABLE "{Schemas.PublicSite}"."Posters" (
            "BusinessTenantId" uuid NOT NULL,
            "Id" uuid NOT NULL,
            "IsPublished" boolean NOT NULL,
            CONSTRAINT "PK_Posters" PRIMARY KEY ("BusinessTenantId", "Id")
        );

        CREATE TABLE "{Schemas.Engagement}"."Deals" (
            "ConsumerTenantId" uuid NOT NULL,
            "BusinessTenantId" uuid NOT NULL,
            "Id" uuid NOT NULL,
            CONSTRAINT "PK_Deals" PRIMARY KEY ("ConsumerTenantId", "BusinessTenantId", "Id")
        );
        CREATE INDEX "IX_Deals_BusinessTenantId_Id" ON "{Schemas.Engagement}"."Deals" ("BusinessTenantId", "Id");

        {RlsSql.EnableTenantRls(Schemas.Tenant, "Widgets")}
        {RlsSql.EnablePublicRls(Schemas.PublicSite, "Posters")}
        {RlsSql.EnablePartiesRls(Schemas.Engagement, "Deals")}

        GRANT SELECT, INSERT, UPDATE, DELETE ON "{Schemas.Tenant}"."Widgets" TO mt_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON "{Schemas.PublicSite}"."Posters" TO mt_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON "{Schemas.Engagement}"."Deals" TO mt_app;
        """;
}
