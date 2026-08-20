using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Procurement
{
    /// <inheritdoc />
    public partial class ThreeWayMatchingAndDeclaredInvoiceEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentDeclaredEvidenceId",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentDeclaredEvidenceVersion",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceDeclaredEvidence",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierInvoiceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SupplierInvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceDeclaredEvidence", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseInvoiceDeclaredEvidence_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceDeclaredEvidence_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceMatchEvaluations",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Lifecycle = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EvaluatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolvedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PurchaseOrderVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    HandoffVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DeclaredEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeclaredEvidenceVersion = table.Column<int>(type: "int", nullable: true),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    ExchangeRateSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: true),
                    VariancesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ReplayResponseSchemaVersion = table.Column<int>(type: "int", nullable: true),
                    ReplayResponseSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceMatchEvaluations", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseInvoiceMatchEvaluations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceMatchEvaluations_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceMatchEvaluations_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceDeclaredEvidenceLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceDeclaredEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TaxRatePercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    NetAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceDeclaredEvidenceLines", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseInvoiceDeclaredEvidenceLines_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceDeclaredEvidenceLines_PurchaseInvoiceDeclaredEvidence_TenantId_PurchaseInvoiceDeclaredEvidenceId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceDeclaredEvidenceId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceDeclaredEvidence",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceDeclaredEvidenceLines_PurchaseOrderLines_TenantId_PurchaseOrderLineId",
                        columns: x => new { x.TenantId, x.PurchaseOrderLineId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceMatchAudit",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceMatchEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReplayResponseSchemaVersion = table.Column<int>(type: "int", nullable: true),
                    ReplayResponseSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceMatchAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceMatchAudit_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceMatchAudit_PurchaseInvoiceMatchEvaluations_TenantId_PurchaseInvoiceMatchEvaluationId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceMatchEvaluationId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceMatchEvaluations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceMatchHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceMatchEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceMatchHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceMatchHistory_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceMatchHistory_PurchaseInvoiceMatchEvaluations_TenantId_PurchaseInvoiceMatchEvaluationId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceMatchEvaluationId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceMatchEvaluations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceDeclaredEvidenceAllocations",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceDeclaredEvidenceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceDeclaredEvidenceAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceDeclaredEvidenceAllocations_GoodsReceiptLines_TenantId_GoodsReceiptLineId",
                        columns: x => new { x.TenantId, x.GoodsReceiptLineId },
                        principalSchema: "procurement",
                        principalTable: "GoodsReceiptLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceDeclaredEvidenceAllocations_PurchaseInvoiceDeclaredEvidenceLines_TenantId_PurchaseInvoiceDeclaredEvidenceLine~",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceDeclaredEvidenceLineId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceDeclaredEvidenceLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidence_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidence",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidence_TenantId_PurchaseInvoiceHandoffId_IsCurrent",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidence",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidence_TenantId_PurchaseInvoiceHandoffId_VersionNumber",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidence",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidenceAllocations_TenantId_GoodsReceiptLineId",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidenceAllocations",
                columns: new[] { "TenantId", "GoodsReceiptLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidenceAllocations_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidenceAllocations",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidenceAllocations_TenantId_PurchaseInvoiceDeclaredEvidenceLineId",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidenceAllocations",
                columns: new[] { "TenantId", "PurchaseInvoiceDeclaredEvidenceLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidenceLines_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidenceLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidenceLines_TenantId_PurchaseInvoiceDeclaredEvidenceId",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidenceLines",
                columns: new[] { "TenantId", "PurchaseInvoiceDeclaredEvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceDeclaredEvidenceLines_TenantId_PurchaseOrderLineId",
                schema: "procurement",
                table: "PurchaseInvoiceDeclaredEvidenceLines",
                columns: new[] { "TenantId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchAudit_TenantId_ActorId_OperationId_IdempotencyKey",
                schema: "procurement",
                table: "PurchaseInvoiceMatchAudit",
                columns: new[] { "TenantId", "ActorId", "OperationId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchAudit_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceMatchAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchAudit_TenantId_PurchaseInvoiceHandoffId",
                schema: "procurement",
                table: "PurchaseInvoiceMatchAudit",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchAudit_TenantId_PurchaseInvoiceMatchEvaluationId_OccurredAt",
                schema: "procurement",
                table: "PurchaseInvoiceMatchAudit",
                columns: new[] { "TenantId", "PurchaseInvoiceMatchEvaluationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchEvaluations_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceMatchEvaluations",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchEvaluations_TenantId_PurchaseInvoiceHandoffId_Lifecycle_EvaluatedAt",
                schema: "procurement",
                table: "PurchaseInvoiceMatchEvaluations",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId", "Lifecycle", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchEvaluations_TenantId_PurchaseInvoiceHandoffId_SourceFingerprint",
                schema: "procurement",
                table: "PurchaseInvoiceMatchEvaluations",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId", "SourceFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchEvaluations_TenantId_PurchaseOrderId_EvaluatedAt",
                schema: "procurement",
                table: "PurchaseInvoiceMatchEvaluations",
                columns: new[] { "TenantId", "PurchaseOrderId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchHistory_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceMatchHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchHistory_TenantId_PurchaseInvoiceHandoffId",
                schema: "procurement",
                table: "PurchaseInvoiceMatchHistory",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceMatchHistory_TenantId_PurchaseInvoiceMatchEvaluationId_OccurredAt",
                schema: "procurement",
                table: "PurchaseInvoiceMatchHistory",
                columns: new[] { "TenantId", "PurchaseInvoiceMatchEvaluationId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseInvoiceDeclaredEvidenceAllocations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceMatchAudit",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceMatchHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceDeclaredEvidenceLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceMatchEvaluations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceDeclaredEvidence",
                schema: "procurement");

            migrationBuilder.DropColumn(
                name: "CurrentDeclaredEvidenceId",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffs");

            migrationBuilder.DropColumn(
                name: "CurrentDeclaredEvidenceVersion",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffs");
        }
    }
}
