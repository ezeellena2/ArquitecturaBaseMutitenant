using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsumerDataRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumerDataRetentions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityTypeKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RetainUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CopyReplacedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumerDataRetentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumerDataRetentions_Tenants_BusinessTenantId",
                        column: x => x.BusinessTenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumerDataRetentions_Tenants_ConsumerTenantId",
                        column: x => x.ConsumerTenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerDataRetentions_BusinessTenantId",
                schema: "identity",
                table: "ConsumerDataRetentions",
                column: "BusinessTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerDataRetentions_ConsumerTenantId",
                schema: "identity",
                table: "ConsumerDataRetentions",
                column: "ConsumerTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerDataRetentions_Pending",
                schema: "identity",
                table: "ConsumerDataRetentions",
                columns: new[] { "RetainUntilUtc", "Id" },
                filter: "\"CopyReplacedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ConsumerDataRetentions_SharedRow",
                schema: "identity",
                table: "ConsumerDataRetentions",
                columns: new[] { "EntityTypeKey", "ConsumerTenantId", "BusinessTenantId", "EntityId" },
                unique: true);

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."ConsumerDataRetentions" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumerDataRetentions",
                schema: "identity");
        }
    }
}
