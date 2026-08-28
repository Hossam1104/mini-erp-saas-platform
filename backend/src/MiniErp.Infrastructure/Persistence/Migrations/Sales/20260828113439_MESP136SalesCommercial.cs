using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP136SalesCommercial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "SalesAudit",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BeforeSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesCreditEvaluations",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OpenReceivables = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    OverdueReceivables = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    NetReceivableExposure = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    ProposedExposure = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OverrideExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCreditEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesHistory",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PolicyId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PolicyVersion = table.Column<int>(type: "int", nullable: true),
                    CreditOutcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesIdempotency",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesIdempotency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SourceQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceQuotationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceQuotationRevision = table.Column<int>(type: "int", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreditOutcome = table.Column<int>(type: "int", nullable: false),
                    CreditReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreditEvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreditOverrideExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    ApprovalPolicyJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesQuotationRevisions",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    SnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesQuotationRevisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesQuotations",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QuotationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CustomerContactId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CustomerReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    ApprovalPolicyJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    CurrentApprovalsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesQuotations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAudit_TenantId_DocumentType_DocumentId_OccurredAt",
                schema: "sales",
                table: "SalesAudit",
                columns: new[] { "TenantId", "DocumentType", "DocumentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAudit_TenantId_Id",
                schema: "sales",
                table: "SalesAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCreditEvaluations_TenantId_DocumentId_EvaluatedAt",
                schema: "sales",
                table: "SalesCreditEvaluations",
                columns: new[] { "TenantId", "DocumentId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCreditEvaluations_TenantId_Id",
                schema: "sales",
                table: "SalesCreditEvaluations",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesHistory_TenantId_DocumentType_DocumentId_OccurredAt",
                schema: "sales",
                table: "SalesHistory",
                columns: new[] { "TenantId", "DocumentType", "DocumentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHistory_TenantId_Id",
                schema: "sales",
                table: "SalesHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesIdempotency_TenantId_Id",
                schema: "sales",
                table: "SalesIdempotency",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesIdempotency_TenantId_OperationId_Key",
                schema: "sales",
                table: "SalesIdempotency",
                columns: new[] { "TenantId", "OperationId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_CompanyId_BranchId_UpdatedAt",
                schema: "sales",
                table: "SalesOrders",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_Id",
                schema: "sales",
                table: "SalesOrders",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_Number",
                schema: "sales",
                table: "SalesOrders",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_SourceQuotationId_SourceQuotationRevision",
                schema: "sales",
                table: "SalesOrders",
                columns: new[] { "TenantId", "SourceQuotationId", "SourceQuotationRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_Status_UpdatedAt",
                schema: "sales",
                table: "SalesOrders",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotationRevisions_TenantId_Id",
                schema: "sales",
                table: "SalesQuotationRevisions",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotationRevisions_TenantId_QuotationId_OccurredAt",
                schema: "sales",
                table: "SalesQuotationRevisions",
                columns: new[] { "TenantId", "QuotationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotationRevisions_TenantId_QuotationId_RevisionNumber",
                schema: "sales",
                table: "SalesQuotationRevisions",
                columns: new[] { "TenantId", "QuotationId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_TenantId_CompanyId_BranchId_UpdatedAt",
                schema: "sales",
                table: "SalesQuotations",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_TenantId_Id",
                schema: "sales",
                table: "SalesQuotations",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_TenantId_Number",
                schema: "sales",
                table: "SalesQuotations",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_TenantId_Status_UpdatedAt",
                schema: "sales",
                table: "SalesQuotations",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesAudit",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesCreditEvaluations",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesHistory",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesIdempotency",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesOrders",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesQuotationRevisions",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesQuotations",
                schema: "sales");

        }
    }
}
