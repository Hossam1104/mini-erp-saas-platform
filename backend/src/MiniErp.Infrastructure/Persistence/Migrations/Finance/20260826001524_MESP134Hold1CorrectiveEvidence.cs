using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP134Hold1CorrectiveEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MonetaryEvidenceJson",
                schema: "finance",
                table: "RevaluationLines",
                type: "nvarchar(max)",
                maxLength: 16384,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostingRuleId",
                schema: "finance",
                table: "RevaluationLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostingRuleVersionNumber",
                schema: "finance",
                table: "RevaluationLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSnapshotJson",
                schema: "finance",
                table: "RevaluationLines",
                type: "nvarchar(max)",
                maxLength: 262144,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "JournalMonetaryEvidence",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TransactionAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
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
                    MonetaryEvidenceJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16384, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalMonetaryEvidence", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalMonetaryEvidence_TenantId_JournalId",
                schema: "finance",
                table: "JournalMonetaryEvidence",
                columns: new[] { "TenantId", "JournalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalMonetaryEvidence",
                schema: "finance");

            migrationBuilder.DropColumn(
                name: "MonetaryEvidenceJson",
                schema: "finance",
                table: "RevaluationLines");

            migrationBuilder.DropColumn(
                name: "PostingRuleId",
                schema: "finance",
                table: "RevaluationLines");

            migrationBuilder.DropColumn(
                name: "PostingRuleVersionNumber",
                schema: "finance",
                table: "RevaluationLines");

            migrationBuilder.DropColumn(
                name: "SourceSnapshotJson",
                schema: "finance",
                table: "RevaluationLines");
        }
    }
}
