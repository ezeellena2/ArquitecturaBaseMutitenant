using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppTemplatesAndRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformWhatsAppTemplates",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Variables = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MetaName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MetaLanguage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Body = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformWhatsAppTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformWhatsAppTemplates_Cultures_Language",
                        column: x => x.Language,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppRates",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character(2)", nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppRates_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalSchema: "platform",
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppRates_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplates",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Variables = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MetaName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MetaLanguage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Body = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplates", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplates_Cultures_Language",
                        column: x => x.Language,
                        principalSchema: "platform",
                        principalTable: "Cultures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplateStates",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WabaId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MetaName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MetaLanguage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MetaTemplateId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplateStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTemplates_Language",
                schema: "platform",
                table: "PlatformWhatsAppTemplates",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTemplates_MetaName",
                schema: "platform",
                table: "PlatformWhatsAppTemplates",
                column: "MetaName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTemplates_Name_Id",
                schema: "platform",
                table: "PlatformWhatsAppTemplates",
                columns: new[] { "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppRates_CountryCode_Category_EffectiveFrom",
                schema: "platform",
                table: "WhatsAppRates",
                columns: new[] { "CountryCode", "Category", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppRates_CurrencyCode",
                schema: "platform",
                table: "WhatsAppRates",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_Language",
                schema: "tenant",
                table: "WhatsAppTemplates",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_TenantId_MetaName",
                schema: "tenant",
                table: "WhatsAppTemplates",
                columns: new[] { "TenantId", "MetaName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_TenantId_Name_Id",
                schema: "tenant",
                table: "WhatsAppTemplates",
                columns: new[] { "TenantId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplateStates_WabaId_MetaName_MetaLanguage",
                schema: "identity",
                table: "WhatsAppTemplateStates",
                columns: new[] { "WabaId", "MetaName", "MetaLanguage" },
                unique: true);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppTemplates");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."WhatsAppTemplates",
                platform."PlatformWhatsAppTemplates",
                identity."WhatsAppTemplateStates" TO mt_app;
                GRANT SELECT, INSERT ON TABLE platform."WhatsAppRates" TO mt_app;
                """);

            // Búsqueda sin acentos por el nombre de la plantilla (paginado-y-busqueda).
            migrationBuilder.Sql("""
                CREATE INDEX "IX_WhatsAppTemplates_Search_Name" ON tenant."WhatsAppTemplates"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                CREATE INDEX "IX_PlatformWhatsAppTemplates_Search_Name" ON platform."PlatformWhatsAppTemplates"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformWhatsAppTemplates",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "WhatsAppRates",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplates",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplateStates",
                schema: "identity");
        }
    }
}
