using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantEditableModelCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalCacheReadAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalCacheWrite1HourAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalCacheWrite5MinutesAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalHighEffort",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalLongCacheReadAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalLongCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalLongCacheWrite1HourAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalLongCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalLongCacheWrite5MinutesAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalLongCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalLongInputAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalLongInputCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalLongInputTokensThreshold",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalLongOutputAmount",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalLongOutputCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalLowEffort",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalMaxInputTokens",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalMaxOutputTokens",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalMediumEffort",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalSupportsAdaptiveThinking",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalSupportsImages",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalSupportsPdf",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalSupportsText",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalSupportsTools",
                schema: "platform",
                table: "AssistantModelPrices",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalLongCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongCacheReadCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalLongCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongCacheWrite1HourCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalLongCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongCacheWrite5MinutesCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalLongInputCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongInputCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantModelPrices_TechnicalLongOutputCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongOutputCurrency");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalCacheWrite1HourCur~",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalCacheWrite5Minutes~",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongCacheReadCurre~",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongCacheReadCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongCacheWrite1Hou~",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongCacheWrite1HourCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongCacheWrite5Min~",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongCacheWrite5MinutesCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongInputCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongInputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongOutputCurrency",
                schema: "platform",
                table: "AssistantModelPrices",
                column: "TechnicalLongOutputCurrency",
                principalSchema: "platform",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
            // Only the four published presets have known metadata. Preserve IDs, base prices and settings.
            // Unknown legacy IDs remain null until an operator completes the technical form.
            migrationBuilder.Sql("""
                WITH published(id, cache5, cache1, cacheRead, threshold, longInput, longOutput, longCache5, longCache1, longCacheRead) AS (
                    VALUES
                    ('claude-fable-5-1', 12.5, 20.0, 0.25, NULL::integer, NULL::numeric, NULL::numeric, NULL::numeric, NULL::numeric, NULL::numeric),
                    ('claude-opus-5-5', 5.0, 8.0, 0.2, NULL, NULL, NULL, NULL, NULL, NULL),
                    ('claude-sonnet-5-5', 2.5, 4.0, 0.1, NULL, NULL, NULL, NULL, NULL, NULL),
                    ('claude-haiku-5-5', 0.125, 0.2, 0.01, 100000, 0.5, 2.5, 0.625, 1.0, 0.05)
                ), changed AS (
                    UPDATE platform."AssistantModelPrices" AS model SET
                        "TechnicalSupportsText" = true, "TechnicalSupportsTools" = true,
                        "TechnicalSupportsImages" = true, "TechnicalSupportsPdf" = true, "TechnicalSupportsAdaptiveThinking" = true,
                        "TechnicalLowEffort" = true, "TechnicalMediumEffort" = true, "TechnicalHighEffort" = true,
                        "TechnicalMaxInputTokens" = 1000000, "TechnicalMaxOutputTokens" = 128000,
                        "TechnicalCacheWrite5MinutesAmount" = published.cache5, "TechnicalCacheWrite5MinutesCurrency" = 'USD',
                        "TechnicalCacheWrite1HourAmount" = published.cache1, "TechnicalCacheWrite1HourCurrency" = 'USD',
                        "TechnicalCacheReadAmount" = published.cacheRead, "TechnicalCacheReadCurrency" = 'USD',
                        "TechnicalLongInputTokensThreshold" = published.threshold,
                        "TechnicalLongInputAmount" = published.longInput,
                        "TechnicalLongInputCurrency" = CASE WHEN published.threshold IS NULL THEN NULL ELSE 'USD' END,
                        "TechnicalLongOutputAmount" = published.longOutput,
                        "TechnicalLongOutputCurrency" = CASE WHEN published.threshold IS NULL THEN NULL ELSE 'USD' END,
                        "TechnicalLongCacheWrite5MinutesAmount" = published.longCache5,
                        "TechnicalLongCacheWrite5MinutesCurrency" = CASE WHEN published.threshold IS NULL THEN NULL ELSE 'USD' END,
                        "TechnicalLongCacheWrite1HourAmount" = published.longCache1,
                        "TechnicalLongCacheWrite1HourCurrency" = CASE WHEN published.threshold IS NULL THEN NULL ELSE 'USD' END,
                        "TechnicalLongCacheReadAmount" = published.longCacheRead,
                        "TechnicalLongCacheReadCurrency" = CASE WHEN published.threshold IS NULL THEN NULL ELSE 'USD' END,
                        "ModifiedAtUtc" = CURRENT_TIMESTAMP, "ModifiedBy" = NULL
                    FROM published
                    WHERE model."Provider" = 'Anthropic' AND model."ProviderModelId" = published.id
                        AND model."TechnicalMaxInputTokens" IS NULL
                    RETURNING model."Id"
                )
                INSERT INTO platform."SecurityEvents" ("Id", "Type", "ActorKind", "ActorId", "Reason", "OccurredAtUtc", "Changes")
                SELECT uuidv7(), 'PlatformSettingsChanged', 'System', '00000000-0000-0000-0000-000000000000'::uuid,
                    'Assistant technical catalog upgrade', CURRENT_TIMESTAMP,
                    '{"assistant.technicalCompleted":{"before":false,"after":true}}'::jsonb
                FROM changed;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes technical data, including later operator edits. Audit events and base prices survive.
            // Downgrading requires a compatible prior binary; Up cannot recover those removed edits.
            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalCacheWrite1HourCur~",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalCacheWrite5Minutes~",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongCacheReadCurre~",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongCacheWrite1Hou~",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongCacheWrite5Min~",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongInputCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantModelPrices_Currencies_TechnicalLongOutputCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalLongCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalLongCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalLongCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalLongInputCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropIndex(
                name: "IX_AssistantModelPrices_TechnicalLongOutputCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalCacheReadAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalCacheWrite1HourAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalCacheWrite5MinutesAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalHighEffort",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongCacheReadAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongCacheReadCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongCacheWrite1HourAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongCacheWrite1HourCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongCacheWrite5MinutesAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongCacheWrite5MinutesCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongInputAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongInputCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongInputTokensThreshold",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongOutputAmount",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLongOutputCurrency",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalLowEffort",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalMaxInputTokens",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalMaxOutputTokens",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalMediumEffort",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalSupportsAdaptiveThinking",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalSupportsImages",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalSupportsPdf",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalSupportsText",
                schema: "platform",
                table: "AssistantModelPrices");

            migrationBuilder.DropColumn(
                name: "TechnicalSupportsTools",
                schema: "platform",
                table: "AssistantModelPrices");
        }
    }
}
