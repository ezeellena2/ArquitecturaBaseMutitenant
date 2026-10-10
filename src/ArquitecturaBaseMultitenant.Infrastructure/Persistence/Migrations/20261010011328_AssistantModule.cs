using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantConversations",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsSaved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantConversations", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantConversations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantModelPrices",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProviderModelId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InputPriceAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    InputPriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OutputPriceAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    OutputPriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantModelPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantNoticeAcceptances",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NoticeVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantNoticeAcceptances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantOrganizationPolicies",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowOwnKey = table.Column<bool>(type: "boolean", nullable: false),
                    DailyMessageLimit = table.Column<int>(type: "integer", nullable: false),
                    MonthlyBudgetAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    MonthlyBudgetCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantOrganizationPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantOrganizationPolicies_Tenants_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantOrganizationSettings",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Effort = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DailyMessageLimit = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MonthlyBudgetAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MonthlyBudgetCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    EncryptedApiKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    MaskedKeySuffix = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantOrganizationSettings", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantOrganizationSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantPlatformSettings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonalEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedModelIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Effort = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DailyMessageLimit = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MonthlyOrganizationBudgetAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    MonthlyOrganizationBudgetCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EncryptedApiKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    MaskedKeySuffix = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantPlatformSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantUsageDaily",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    CostAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CostCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantUsageDaily", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantUsageDaily_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAssistantConversations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsSaved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAssistantUsageDaily",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    CostAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CostCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantUsageDaily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantMessages",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Text = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: true),
                    PositiveFeedback = table.Column<bool>(type: "boolean", nullable: true),
                    ToolKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ToolDurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    ToolState = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantMessages", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_AssistantMessages_TenantId_ConversationId_Id", x => new { x.TenantId, x.ConversationId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantMessages_AssistantConversations_TenantId_Conversat~",
                        columns: x => new { x.TenantId, x.ConversationId },
                        principalSchema: "tenant",
                        principalTable: "AssistantConversations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantMessages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantTurns",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantTurns", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantTurns_AssistantConversations_TenantId_Conversation~",
                        columns: x => new { x.TenantId, x.ConversationId },
                        principalSchema: "tenant",
                        principalTable: "AssistantConversations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantTurns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAssistantMessages",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Text = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: true),
                    PositiveFeedback = table.Column<bool>(type: "boolean", nullable: true),
                    ToolKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ToolDurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    ToolState = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantMessages", x => x.Id);
                    table.UniqueConstraint("AK_PlatformAssistantMessages_ConversationId_Id", x => new { x.ConversationId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlatformAssistantMessages_PlatformAssistantConversations_Co~",
                        column: x => x.ConversationId,
                        principalSchema: "platform",
                        principalTable: "PlatformAssistantConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAssistantTurns",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformAssistantTurns_PlatformAssistantConversations_Conve~",
                        column: x => x.ConversationId,
                        principalSchema: "platform",
                        principalTable: "PlatformAssistantConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantAttachments",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantAttachments", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantAttachments_AssistantConversations_TenantId_Conver~",
                        columns: x => new { x.TenantId, x.ConversationId },
                        principalSchema: "tenant",
                        principalTable: "AssistantConversations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantAttachments_AssistantMessages_TenantId_Conversatio~",
                        columns: x => new { x.TenantId, x.ConversationId, x.MessageId },
                        principalSchema: "tenant",
                        principalTable: "AssistantMessages",
                        principalColumns: new[] { "TenantId", "ConversationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantAttachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAssistantAttachments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformAssistantAttachments_PlatformAssistantConversations~",
                        column: x => x.ConversationId,
                        principalSchema: "platform",
                        principalTable: "PlatformAssistantConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlatformAssistantAttachments_PlatformAssistantMessages_Conv~",
                        columns: x => new { x.ConversationId, x.MessageId },
                        principalSchema: "platform",
                        principalTable: "PlatformAssistantMessages",
                        principalColumns: new[] { "ConversationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantAttachments_TenantId_ConversationId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "AssistantAttachments",
                columns: new[] { "TenantId", "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantAttachments_TenantId_ConversationId_MessageId",
                schema: "tenant",
                table: "AssistantAttachments",
                columns: new[] { "TenantId", "ConversationId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversations_TenantId_LastActivityAtUtc_Id",
                schema: "tenant",
                table: "AssistantConversations",
                columns: new[] { "TenantId", "LastActivityAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversations_TenantId_OwnerUserId_Access_LastActi~",
                schema: "tenant",
                table: "AssistantConversations",
                columns: new[] { "TenantId", "OwnerUserId", "Access", "LastActivityAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantMessages_TenantId_ConversationId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "AssistantMessages",
                columns: new[] { "TenantId", "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_Provider_ProviderModelId",
                schema: "platform",
                table: "AssistantModelPrices",
                columns: new[] { "Provider", "ProviderModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoticeAcceptances_OwnerUserId_Access_NoticeVersion",
                schema: "platform",
                table: "AssistantNoticeAcceptances",
                columns: new[] { "OwnerUserId", "Access", "NoticeVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantOrganizationPolicies_OrganizationId",
                schema: "platform",
                table: "AssistantOrganizationPolicies",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantOrganizationSettings_TenantId",
                schema: "tenant",
                table: "AssistantOrganizationSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurns_TenantId_ConversationId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "AssistantTurns",
                columns: new[] { "TenantId", "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageDaily_TenantId_UserId_Day",
                schema: "tenant",
                table: "AssistantUsageDaily",
                columns: new[] { "TenantId", "UserId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantAttachments_ConversationId_CreatedAtUtc_Id",
                schema: "platform",
                table: "PlatformAssistantAttachments",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantAttachments_ConversationId_MessageId",
                schema: "platform",
                table: "PlatformAssistantAttachments",
                columns: new[] { "ConversationId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantConversations_OwnerUserId_LastActivityAtUt~",
                schema: "platform",
                table: "PlatformAssistantConversations",
                columns: new[] { "OwnerUserId", "LastActivityAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantMessages_ConversationId_CreatedAtUtc_Id",
                schema: "platform",
                table: "PlatformAssistantMessages",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantTurns_ConversationId_CreatedAtUtc_Id",
                schema: "platform",
                table: "PlatformAssistantTurns",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantUsageDaily_UserId_Day",
                schema: "platform",
                table: "PlatformAssistantUsageDaily",
                columns: new[] { "UserId", "Day" },
                unique: true);
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantConversations");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantMessages");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantAttachments");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantTurns");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantOrganizationSettings");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantUsageDaily");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."AssistantAttachments",
                tenant."AssistantConversations",
                tenant."AssistantMessages",
                tenant."AssistantOrganizationSettings",
                tenant."AssistantTurns",
                tenant."AssistantUsageDaily",
                platform."AssistantModelPrices",
                platform."AssistantNoticeAcceptances",
                platform."AssistantOrganizationPolicies",
                platform."AssistantPlatformSettings",
                platform."PlatformAssistantAttachments",
                platform."PlatformAssistantConversations",
                platform."PlatformAssistantMessages",
                platform."PlatformAssistantTurns",
                platform."PlatformAssistantUsageDaily" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantAttachments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssistantModelPrices",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantNoticeAcceptances",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantOrganizationPolicies",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantOrganizationSettings",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssistantPlatformSettings",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantTurns",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssistantUsageDaily",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PlatformAssistantAttachments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformAssistantTurns",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformAssistantUsageDaily",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantMessages",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PlatformAssistantMessages",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantConversations",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PlatformAssistantConversations",
                schema: "platform");
        }
    }
}
