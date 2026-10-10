using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CostAmount",
                schema: "platform",
                table: "PlatformAssistantUsageDaily",
                type: "numeric(24,12)",
                precision: 24,
                scale: 12,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "CostAmount",
                schema: "tenant",
                table: "AssistantUsageDaily",
                type: "numeric(24,12)",
                precision: 24,
                scale: 12,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.CreateTable(
                name: "AssistantBudgetMonths",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantBudgetMonths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantBudgetMonths_TimeZones_TimeZoneId",
                        column: x => x.TimeZoneId,
                        principalSchema: "platform",
                        principalTable: "TimeZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantOwnKeyDailyQuotas",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservedQueries = table.Column<int>(type: "integer", nullable: false),
                    ConsumedQueries = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantOwnKeyDailyQuotas", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantOwnKeyDailyQuotas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantPlatformKeyDailyQuotas",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservedQueries = table.Column<int>(type: "integer", nullable: false),
                    ConsumedQueries = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantPlatformKeyDailyQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantUsageFacts",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallId = table.Column<Guid>(type: "uuid", nullable: false),
                    Funding = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DayUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceRevision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheReadTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheWriteTokens = table.Column<long>(type: "bigint", nullable: false),
                    IsTitle = table.Column<bool>(type: "boolean", nullable: false),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    CostAmount = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    CostCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantUsageFacts", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantUsageFacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantFinancialOperations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Funding = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DayUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    DailyLimit = table.Column<int>(type: "integer", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: true),
                    MonthlyBudgetAmount = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: true),
                    MonthlyBudgetCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    ReservedCostAmount = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    ReservedCostCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    Price_ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price_ProviderModelId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price_Revision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price_BillingCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceInputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceInputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceOutputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceOutputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceCacheWrite5MinutesAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceCacheWrite5MinutesCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceCacheWrite1HourAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceCacheWrite1HourCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceCacheReadAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceCacheReadCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    Price_LongPromptThreshold = table.Column<int>(type: "integer", nullable: true),
                    PriceLongInputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongInputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongOutputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongOutputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongCacheWrite5MinutesAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongCacheWrite5MinutesCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongCacheWrite1HourAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongCacheWrite1HourCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongCacheReadAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongCacheReadCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePrice_ModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    TitlePrice_ProviderModelId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TitlePrice_Revision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TitlePrice_BillingCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceInputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceInputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceOutputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceOutputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceCacheWrite5MinutesAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceCacheWrite5MinutesCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceCacheWrite1HourAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceCacheWrite1HourCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceCacheReadAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceCacheReadCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePrice_LongPromptThreshold = table.Column<int>(type: "integer", nullable: true),
                    TitlePriceLongInputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceLongInputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceLongOutputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceLongOutputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceLongCacheWrite5MinutesAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceLongCacheWrite5MinutesCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceLongCacheWrite1HourAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceLongCacheWrite1HourCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    TitlePriceLongCacheReadAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TitlePriceLongCacheReadCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContentDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantFinancialOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantFinancialOperations_AssistantBudgetMonths_BudgetMo~",
                        column: x => x.BudgetMonthId,
                        principalSchema: "platform",
                        principalTable: "AssistantBudgetMonths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantFinancialOperations_Currencies_Price_BillingCurren~",
                        column: x => x.Price_BillingCurrency,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantFinancialOperations_Currencies_TitlePrice_BillingC~",
                        column: x => x.TitlePrice_BillingCurrency,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantFinancialCalls",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsTitle = table.Column<bool>(type: "boolean", nullable: false),
                    Price_ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price_ProviderModelId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price_Revision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price_BillingCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceInputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceInputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceOutputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceOutputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceCacheWrite5MinutesAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceCacheWrite5MinutesCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceCacheWrite1HourAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceCacheWrite1HourCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    PriceCacheReadAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PriceCacheReadCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                    Price_LongPromptThreshold = table.Column<int>(type: "integer", nullable: true),
                    PriceLongInputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongInputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongOutputAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongOutputCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongCacheWrite5MinutesAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongCacheWrite5MinutesCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongCacheWrite1HourAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongCacheWrite1HourCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    PriceLongCacheReadAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceLongCacheReadCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheReadTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheWrite5MinutesTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheWrite1HourTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheWriteUnknownTokens = table.Column<long>(type: "bigint", nullable: false),
                    ReasoningTokens = table.Column<long>(type: "bigint", nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                    CostAmount = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    CostCurrency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantFinancialCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantFinancialCalls_AssistantFinancialOperations_Operat~",
                        column: x => x.OperationId,
                        principalSchema: "platform",
                        principalTable: "AssistantFinancialOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantFinancialCalls_Currencies_Price_BillingCurrency",
                        column: x => x.Price_BillingCurrency,
                        principalSchema: "platform",
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBudgetMonths_OrganizationId_Month",
                schema: "platform",
                table: "AssistantBudgetMonths",
                columns: new[] { "OrganizationId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBudgetMonths_OrganizationId_StartUtc_EndUtc",
                schema: "platform",
                table: "AssistantBudgetMonths",
                columns: new[] { "OrganizationId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantBudgetMonths_TimeZoneId",
                schema: "platform",
                table: "AssistantBudgetMonths",
                column: "TimeZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_OperationId_Sequence",
                schema: "platform",
                table: "AssistantFinancialCalls",
                columns: new[] { "OperationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_Price_BillingCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "Price_BillingCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_Access_ScopeTenantId_CreatedAt~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                columns: new[] { "Access", "ScopeTenantId", "CreatedAtUtc", "Funding" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_BudgetMonthId_Funding_State",
                schema: "platform",
                table: "AssistantFinancialOperations",
                columns: new[] { "BudgetMonthId", "Funding", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_Price_BillingCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "Price_BillingCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePrice_BillingCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePrice_BillingCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_UserId_DayUtc",
                schema: "platform",
                table: "AssistantFinancialOperations",
                columns: new[] { "UserId", "DayUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantOwnKeyDailyQuotas_TenantId_UserId_DayUtc",
                schema: "tenant",
                table: "AssistantOwnKeyDailyQuotas",
                columns: new[] { "TenantId", "UserId", "DayUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantPlatformKeyDailyQuotas_UserId_DayUtc",
                schema: "platform",
                table: "AssistantPlatformKeyDailyQuotas",
                columns: new[] { "UserId", "DayUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageFacts_TenantId_BudgetMonthId_Funding",
                schema: "tenant",
                table: "AssistantUsageFacts",
                columns: new[] { "TenantId", "BudgetMonthId", "Funding" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageFacts_TenantId_CallId",
                schema: "tenant",
                table: "AssistantUsageFacts",
                columns: new[] { "TenantId", "CallId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageFacts_TenantId_DayUtc_Funding_UserId",
                schema: "tenant",
                table: "AssistantUsageFacts",
                columns: new[] { "TenantId", "DayUtc", "Funding", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageFacts_CostCurrency",
                schema: "tenant",
                table: "AssistantUsageFacts",
                column: "CostCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_MonthlyBudgetCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "MonthlyBudgetCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceCacheReadCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceInputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceInputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceLongCacheReadCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceLongCacheWrite1HourCurren~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceLongCacheWrite5MinutesCur~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceLongInputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongInputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceLongOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongOutputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_PriceOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceOutputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_ReservedCostCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "ReservedCostCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceCacheReadCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceCacheWrite1HourCurre~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceCacheWrite5MinutesCu~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceInputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceInputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceLongCacheReadCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceLongCacheWrite1HourC~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceLongCacheWrite5Minut~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceLongInputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongInputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceLongOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongOutputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialOperations_TitlePriceOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceOutputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_CostCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "CostCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceCacheReadCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceInputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceInputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceLongCacheReadCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceLongCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceLongCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceLongInputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongInputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceLongOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongOutputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFinancialCalls_PriceOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceOutputCurrency");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_CostCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "CostCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceCacheReadCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceCacheWrite1HourCurr~",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceCacheWrite5MinutesC~",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceInputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceInputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceLongCacheReadCurren~",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceLongCacheWrite1Hour~",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceLongCacheWrite5Minu~",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceLongInputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongInputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceLongOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceLongOutputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialCalls_Currencies_PriceOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialCalls",
                column: "PriceOutputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_MonthlyBudgetCurren~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "MonthlyBudgetCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceCacheReadCurre~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceCacheWrite1Hou~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceCacheWrite5Min~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceInputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceInputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceLongCacheReadC~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceLongCacheWrite~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceLongCacheWrit~1",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceLongInputCurre~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongInputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceLongOutputCurr~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceLongOutputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_PriceOutputCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "PriceOutputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_ReservedCostCurrency",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "ReservedCostCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceCacheRead~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceCacheWrit~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceCacheWri~1",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceInputCurr~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceInputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceLongCache~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceLongCach~1",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceLongCach~2",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceLongInput~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongInputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceLongOutpu~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceLongOutputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantFinancialOperations_Currencies_TitlePriceOutputCur~",
                schema: "platform",
                table: "AssistantFinancialOperations",
                column: "TitlePriceOutputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantUsageFacts_Currencies_CostCurrency",
                schema: "tenant",
                table: "AssistantUsageFacts",
                column: "CostCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantOwnKeyDailyQuotas");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantUsageFacts");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                    platform."AssistantBudgetMonths", platform."AssistantFinancialOperations",
                    platform."AssistantFinancialCalls", platform."AssistantPlatformKeyDailyQuotas",
                    tenant."AssistantOwnKeyDailyQuotas", tenant."AssistantUsageFacts"
                TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantFinancialCalls",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantOwnKeyDailyQuotas",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssistantPlatformKeyDailyQuotas",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantUsageFacts",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssistantFinancialOperations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AssistantBudgetMonths",
                schema: "platform");

            migrationBuilder.AlterColumn<decimal>(
                name: "CostAmount",
                schema: "platform",
                table: "PlatformAssistantUsageDaily",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,12)",
                oldPrecision: 24,
                oldScale: 12);

            migrationBuilder.AlterColumn<decimal>(
                name: "CostAmount",
                schema: "tenant",
                table: "AssistantUsageDaily",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,12)",
                oldPrecision: 24,
                oldScale: 12);
        }
    }
}
