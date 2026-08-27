using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP135FinanceCloseReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeriodCloseEvidence",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChecksJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodVersionJson = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodCloseEvidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodCloseRuns",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReadinessStatus = table.Column<int>(type: "int", nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ChecksJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReopenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReopenedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodCloseRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodHistory",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    FromState = table.Column<int>(type: "int", nullable: false),
                    ToState = table.Column<int>(type: "int", nullable: false),
                    CloseRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearEndRuns",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClosingJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetainedEarningsAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetainedEarningsAccountCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PostingRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingRuleVersionNumber = table.Column<int>(type: "int", nullable: true),
                    PostingRuleSourceContract = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PostingRuleSourceEvent = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearEndRuns", x => x.Id);
                    table.UniqueConstraint("AK_YearEndRuns_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "YearEndLines",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AccountNameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    NetBalance = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ClosingJournalLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearEndLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearEndLines_YearEndRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "finance",
                        principalTable: "YearEndRuns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodCloseEvidence_TenantId_CompanyId_EvaluatedAt",
                schema: "finance",
                table: "PeriodCloseEvidence",
                columns: new[] { "TenantId", "CompanyId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodCloseEvidence_TenantId_PeriodId_SnapshotFingerprint",
                schema: "finance",
                table: "PeriodCloseEvidence",
                columns: new[] { "TenantId", "PeriodId", "SnapshotFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodCloseRuns_TenantId_CompanyId_PeriodId_Status",
                schema: "finance",
                table: "PeriodCloseRuns",
                columns: new[] { "TenantId", "CompanyId", "PeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodCloseRuns_TenantId_PeriodId_Sequence",
                schema: "finance",
                table: "PeriodCloseRuns",
                columns: new[] { "TenantId", "PeriodId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodHistory_TenantId_PeriodId_OccurredAt",
                schema: "finance",
                table: "PeriodHistory",
                columns: new[] { "TenantId", "PeriodId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_YearEndLines_TenantId_RunId_AccountId",
                schema: "finance",
                table: "YearEndLines",
                columns: new[] { "TenantId", "RunId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearEndRuns_TenantId_CompanyId_FiscalYearId_SnapshotFingerprint",
                schema: "finance",
                table: "YearEndRuns",
                columns: new[] { "TenantId", "CompanyId", "FiscalYearId", "SnapshotFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearEndRuns_TenantId_CompanyId_FiscalYearId_Status",
                schema: "finance",
                table: "YearEndRuns",
                columns: new[] { "TenantId", "CompanyId", "FiscalYearId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PeriodCloseEvidence",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "PeriodCloseRuns",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "PeriodHistory",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "YearEndLines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "YearEndRuns",
                schema: "finance");
        }
    }
}
