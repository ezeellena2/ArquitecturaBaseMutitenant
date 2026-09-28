using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenant");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "public_site");

            migrationBuilder.EnsureSchema(
                name: "engagement");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent WITH SCHEMA public;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;");
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.f_unaccent(text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT AS $function$
                    SELECT public.unaccent('public.unaccent'::regdictionary, $1)
                $function$;
                """);
            migrationBuilder.CreateRlsSupportFunctions();

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorKind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "platform",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character(3)", nullable: false),
                    NumericCode = table.Column<string>(type: "character(3)", nullable: false),
                    MinorUnits = table.Column<int>(type: "integer", nullable: true),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    BodyHash = table.Column<string>(type: "text", nullable: false),
                    Route = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeZones",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                schema: "platform",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character(2)", nullable: false),
                    Alpha3 = table.Column<string>(type: "character(3)", nullable: false),
                    NumericCode = table.Column<string>(type: "character(3)", nullable: false),
                    CallingCode = table.Column<string>(type: "text", nullable: true),
                    DefaultCurrencyCode = table.Column<string>(type: "character(3)", nullable: true),
                    DefaultTimeZoneId = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Countries_Currencies_DefaultCurrencyCode",
                        column: x => x.DefaultCurrencyCode,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Countries_TimeZones_DefaultTimeZoneId",
                        column: x => x.DefaultTimeZoneId,
                        principalSchema: "platform",
                        principalTable: "TimeZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cultures",
                schema: "platform",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "character(2)", nullable: false),
                    DatePattern = table.Column<string>(type: "text", nullable: false),
                    TimePattern = table.Column<string>(type: "text", nullable: false),
                    DateTimePattern = table.Column<string>(type: "text", nullable: false),
                    LongDatePattern = table.Column<string>(type: "text", nullable: false),
                    DecimalSeparator = table.Column<string>(type: "text", nullable: false),
                    GroupSeparator = table.Column<string>(type: "text", nullable: false),
                    CurrencyPattern = table.Column<string>(type: "text", nullable: false),
                    PercentPattern = table.Column<string>(type: "text", nullable: false),
                    FallbackCulture = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cultures", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Cultures_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalSchema: "platform",
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cultures_Cultures_FallbackCulture",
                        column: x => x.FallbackCulture,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxIdTypes",
                schema: "platform",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "character(2)", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Mask = table.Column<string>(type: "text", nullable: false),
                    ValidatorKey = table.Column<string>(type: "text", nullable: false),
                    AppliesTo = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxIdTypes", x => x.Code);
                    table.ForeignKey(
                        name: "FK_TaxIdTypes_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalSchema: "platform",
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeZoneCountries",
                schema: "platform",
                columns: table => new
                {
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "character(2)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeZoneCountries", x => new { x.TimeZoneId, x.CountryCode });
                    table.ForeignKey(
                        name: "FK_TimeZoneCountries_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalSchema: "platform",
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeZoneCountries_TimeZones_TimeZoneId",
                        column: x => x.TimeZoneId,
                        principalSchema: "platform",
                        principalTable: "TimeZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CountryTranslations",
                schema: "platform",
                columns: table => new
                {
                    CountryCode = table.Column<string>(type: "character(2)", nullable: false),
                    Culture = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryTranslations", x => new { x.CountryCode, x.Culture });
                    table.ForeignKey(
                        name: "FK_CountryTranslations_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalSchema: "platform",
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CountryTranslations_Cultures_Culture",
                        column: x => x.Culture,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CultureTranslations",
                schema: "platform",
                columns: table => new
                {
                    CultureCode = table.Column<string>(type: "text", nullable: false),
                    DisplayCulture = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CultureTranslations", x => new { x.CultureCode, x.DisplayCulture });
                    table.ForeignKey(
                        name: "FK_CultureTranslations_Cultures_CultureCode",
                        column: x => x.CultureCode,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CultureTranslations_Cultures_DisplayCulture",
                        column: x => x.DisplayCulture,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyTranslations",
                schema: "platform",
                columns: table => new
                {
                    CurrencyCode = table.Column<string>(type: "character(3)", nullable: false),
                    Culture = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NamePlural = table.Column<string>(type: "text", nullable: false),
                    DisplaySymbol = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyTranslations", x => new { x.CurrencyCode, x.Culture });
                    table.ForeignKey(
                        name: "FK_CurrencyTranslations_Cultures_Culture",
                        column: x => x.Culture,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrencyTranslations_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeZoneTranslations",
                schema: "platform",
                columns: table => new
                {
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    Culture = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeZoneTranslations", x => new { x.TimeZoneId, x.Culture });
                    table.ForeignKey(
                        name: "FK_TimeZoneTranslations_Cultures_Culture",
                        column: x => x.Culture,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeZoneTranslations_TimeZones_TimeZoneId",
                        column: x => x.TimeZoneId,
                        principalSchema: "platform",
                        principalTable: "TimeZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxIdTypeTranslations",
                schema: "platform",
                columns: table => new
                {
                    TaxIdTypeCode = table.Column<string>(type: "text", nullable: false),
                    Culture = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxIdTypeTranslations", x => new { x.TaxIdTypeCode, x.Culture });
                    table.ForeignKey(
                        name: "FK_TaxIdTypeTranslations_Cultures_Culture",
                        column: x => x.Culture,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxIdTypeTranslations_TaxIdTypes_TaxIdTypeCode",
                        column: x => x.TaxIdTypeCode,
                        principalSchema: "platform",
                        principalTable: "TaxIdTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_OccurredAtUtc_Id",
                schema: "tenant",
                table: "AuditEntries",
                columns: new[] { "TenantId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_DefaultCurrencyCode",
                schema: "platform",
                table: "Countries",
                column: "DefaultCurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_DefaultTimeZoneId",
                schema: "platform",
                table: "Countries",
                column: "DefaultTimeZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_CountryTranslations_Culture",
                schema: "platform",
                table: "CountryTranslations",
                column: "Culture");

            migrationBuilder.CreateIndex(
                name: "IX_Cultures_CountryCode",
                schema: "platform",
                table: "Cultures",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_Cultures_FallbackCulture",
                schema: "platform",
                table: "Cultures",
                column: "FallbackCulture");

            migrationBuilder.CreateIndex(
                name: "IX_CultureTranslations_DisplayCulture",
                schema: "platform",
                table: "CultureTranslations",
                column: "DisplayCulture");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyTranslations_Culture",
                schema: "platform",
                table: "CurrencyTranslations",
                column: "Culture");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAtUtc",
                schema: "platform",
                table: "IdempotencyKeys",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_TenantId_UserId_Key",
                schema: "platform",
                table: "IdempotencyKeys",
                columns: new[] { "TenantId", "UserId", "Key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_TaxIdTypes_CountryCode",
                schema: "platform",
                table: "TaxIdTypes",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_TaxIdTypeTranslations_Culture",
                schema: "platform",
                table: "TaxIdTypeTranslations",
                column: "Culture");

            migrationBuilder.CreateIndex(
                name: "IX_TimeZoneCountries_CountryCode",
                schema: "platform",
                table: "TimeZoneCountries",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_TimeZoneTranslations_Culture",
                schema: "platform",
                table: "TimeZoneTranslations",
                column: "Culture");

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AuditEntries");
            migrationBuilder.PreventUpdateDelete(Schemas.Tenant, "AuditEntries");

            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA platform, identity, tenant, public_site, engagement TO mt_app;
                GRANT USAGE ON SCHEMA public TO mt_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA platform, identity, tenant, public_site, engagement TO mt_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA platform, identity, tenant, public_site, engagement TO mt_app;
                GRANT EXECUTE ON FUNCTION public.f_unaccent(text) TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "CountryTranslations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CultureTranslations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CurrencyTranslations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TaxIdTypeTranslations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TimeZoneCountries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TimeZoneTranslations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TaxIdTypes",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Cultures",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Countries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TimeZones",
                schema: "platform");

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS public.f_unaccent(text);
                DROP FUNCTION IF EXISTS platform.prevent_update_delete();
                DROP FUNCTION IF EXISTS platform.prevent_tenant_change();
                DROP EXTENSION IF EXISTS pg_trgm;
                DROP EXTENSION IF EXISTS unaccent;
                """);

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS engagement;
                DROP SCHEMA IF EXISTS public_site;
                DROP SCHEMA IF EXISTS identity;
                DROP SCHEMA IF EXISTS tenant;
                DROP SCHEMA IF EXISTS platform;
                """);
        }
    }
}
