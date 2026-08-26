using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP134TaxFxReportingRevaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HistoricalFunctionalAmount",
                schema: "finance",
                table: "Allocations",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RealizedFxAmount",
                schema: "finance",
                table: "Allocations",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RealizedFxDirection",
                schema: "finance",
                table: "Allocations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RealizedFxJournalId",
                schema: "finance",
                table: "Allocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RealizedFxRuleId",
                schema: "finance",
                table: "Allocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RealizedFxRuleVersionNumber",
                schema: "finance",
                table: "Allocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementFunctionalAmount",
                schema: "finance",
                table: "Allocations",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MonetaryPolicies",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReportingCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportingCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    RoundingScale = table.Column<int>(type: "int", nullable: false),
                    RoundingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RevaluationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonetaryPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevaluationBatches",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevaluationBatches", x => x.Id);
                    table.UniqueConstraint("AK_RevaluationBatches_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "TaxAccountingEffects",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TaxRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxRateVersionNumber = table.Column<int>(type: "int", nullable: false),
                    TaxEffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TaxRatePercentage = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    TaxableBase = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReportingCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ReportingAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TransactionToFunctionalRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransactionToFunctionalRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransactionToFunctionalRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    FunctionalToReportingRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FunctionalToReportingRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FunctionalToReportingRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    SourceUnroundedFunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    SourceUnroundedReportingAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    RoundingScale = table.Column<int>(type: "int", nullable: false),
                    RoundingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FunctionalRoundingDifference = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ReportingRoundingDifference = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    ReportingEvidenceStatus = table.Column<int>(type: "int", nullable: false),
                    JournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReversalJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingRuleVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxAccountingEffects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevaluationLines",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OutstandingTransactionAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    HistoricalFunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    RevaluedFunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeRateVersionNumber = table.Column<int>(type: "int", nullable: false),
                    ExchangeSourceCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExchangeTargetCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExchangeEffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    ExchangeRateScale = table.Column<int>(type: "int", nullable: false),
                    ExchangeProvenance = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExchangeSourceNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    JournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevaluationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevaluationLines_RevaluationBatches_TenantId_BatchId",
                        columns: x => new { x.TenantId, x.BatchId },
                        principalSchema: "finance",
                        principalTable: "RevaluationBatches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonetaryPolicies_TenantId_CompanyId_EffectiveFrom",
                schema: "finance",
                table: "MonetaryPolicies",
                columns: new[] { "TenantId", "CompanyId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_MonetaryPolicies_TenantId_CompanyId_VersionNumber",
                schema: "finance",
                table: "MonetaryPolicies",
                columns: new[] { "TenantId", "CompanyId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevaluationBatches_TenantId_CompanyId_AsOfDate_Status",
                schema: "finance",
                table: "RevaluationBatches",
                columns: new[] { "TenantId", "CompanyId", "AsOfDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RevaluationLines_TenantId_BatchId_SourceId",
                schema: "finance",
                table: "RevaluationLines",
                columns: new[] { "TenantId", "BatchId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevaluationLines_TenantId_CompanyId_SourceId_Status",
                schema: "finance",
                table: "RevaluationLines",
                columns: new[] { "TenantId", "CompanyId", "SourceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxAccountingEffects_TenantId_CompanyId_JournalId",
                schema: "finance",
                table: "TaxAccountingEffects",
                columns: new[] { "TenantId", "CompanyId", "JournalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxAccountingEffects_TenantId_CompanyId_OpenItemId",
                schema: "finance",
                table: "TaxAccountingEffects",
                columns: new[] { "TenantId", "CompanyId", "OpenItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonetaryPolicies",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "RevaluationLines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "TaxAccountingEffects",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "RevaluationBatches",
                schema: "finance");

            migrationBuilder.DropColumn(
                name: "HistoricalFunctionalAmount",
                schema: "finance",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "RealizedFxAmount",
                schema: "finance",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "RealizedFxDirection",
                schema: "finance",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "RealizedFxJournalId",
                schema: "finance",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "RealizedFxRuleId",
                schema: "finance",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "RealizedFxRuleVersionNumber",
                schema: "finance",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "SettlementFunctionalAmount",
                schema: "finance",
                table: "Allocations");
        }
    }
}
