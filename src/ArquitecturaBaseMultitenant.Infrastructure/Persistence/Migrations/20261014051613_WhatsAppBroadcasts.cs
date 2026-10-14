using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppBroadcasts",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AudienceKind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AudienceTag = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Recipients = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCostAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    EstimatedCostCurrency = table.Column<string>(type: "character(3)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Variables = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppBroadcasts", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppBroadcasts_Currencies_EstimatedCostCurrency",
                        column: x => x.EstimatedCostCurrency,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppBroadcasts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppBroadcastRecipients",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppBroadcastRecipients", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppBroadcastRecipients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppBroadcastRecipients_WhatsAppBroadcasts_TenantId_Bro~",
                        columns: x => new { x.TenantId, x.BroadcastId },
                        principalSchema: "tenant",
                        principalTable: "WhatsAppBroadcasts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcastRecipients_OutboxMessageId",
                schema: "tenant",
                table: "WhatsAppBroadcastRecipients",
                column: "OutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcastRecipients_TenantId_BroadcastId_Status",
                schema: "tenant",
                table: "WhatsAppBroadcastRecipients",
                columns: new[] { "TenantId", "BroadcastId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcastRecipients_TenantId_CustomerId_SentAtUtc",
                schema: "tenant",
                table: "WhatsAppBroadcastRecipients",
                columns: new[] { "TenantId", "CustomerId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcasts_EstimatedCostCurrency",
                schema: "tenant",
                table: "WhatsAppBroadcasts",
                column: "EstimatedCostCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_Name_Id",
                schema: "tenant",
                table: "WhatsAppBroadcasts",
                columns: new[] { "TenantId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_Status_ScheduledAtUtc",
                schema: "tenant",
                table: "WhatsAppBroadcasts",
                columns: new[] { "TenantId", "Status", "ScheduledAtUtc" });

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppBroadcasts");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppBroadcastRecipients");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."WhatsAppBroadcasts",
                tenant."WhatsAppBroadcastRecipients" TO mt_app;
                """);

            // Búsqueda sin acentos por el nombre de la difusión y por el número del destinatario (paginado-y-busqueda).
            migrationBuilder.Sql("""
                CREATE INDEX "IX_WhatsAppBroadcasts_Search_Name" ON tenant."WhatsAppBroadcasts"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                CREATE INDEX "IX_WhatsAppBroadcastRecipients_Search_WaId" ON tenant."WhatsAppBroadcastRecipients"
                    USING gin (public.f_unaccent(lower("WaId")) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppBroadcastRecipients",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "WhatsAppBroadcasts",
                schema: "tenant");
        }
    }
}
