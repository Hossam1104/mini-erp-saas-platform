using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Procurement
{
    /// <inheritdoc />
    public partial class MESP127SupplierReturnEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierReturns",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    CommercialOutcome = table.Column<int>(type: "int", nullable: false),
                    ReasonDetail = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CorrectionOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryHandoffEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryHandoffReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InventoryHandoffRecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinanceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FinanceCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FinanceAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    FinanceReferenceRecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturns", x => x.Id);
                    table.UniqueConstraint("AK_SupplierReturns_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupplierReturns_GoodsReceipts_TenantId_GoodsReceiptId",
                        columns: x => new { x.TenantId, x.GoodsReceiptId },
                        principalSchema: "procurement",
                        principalTable: "GoodsReceipts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnAudit",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationPath = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    BeforeStatus = table.Column<int>(type: "int", nullable: true),
                    AfterStatus = table.Column<int>(type: "int", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforeSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReplayResponseSchemaVersion = table.Column<int>(type: "int", nullable: true),
                    ReplayResponseSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnAudit_SupplierReturns_TenantId_SupplierReturnId",
                        columns: x => new { x.TenantId, x.SupplierReturnId },
                        principalSchema: "procurement",
                        principalTable: "SupplierReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnEvidence",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnEvidence_SupplierReturns_TenantId_SupplierReturnId",
                        columns: x => new { x.TenantId, x.SupplierReturnId },
                        principalSchema: "procurement",
                        principalTable: "SupplierReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnHistory_SupplierReturns_TenantId_SupplierReturnId",
                        columns: x => new { x.TenantId, x.SupplierReturnId },
                        principalSchema: "procurement",
                        principalTable: "SupplierReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AcceptedQuantityAtReturn = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ReturnQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    EligibleQuantityAfter = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_GoodsReceiptLines_TenantId_GoodsReceiptLineId",
                        columns: x => new { x.TenantId, x.GoodsReceiptLineId },
                        principalSchema: "procurement",
                        principalTable: "GoodsReceiptLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_PurchaseOrderLines_TenantId_PurchaseOrderLineId",
                        columns: x => new { x.TenantId, x.PurchaseOrderLineId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_SupplierReturns_TenantId_SupplierReturnId",
                        columns: x => new { x.TenantId, x.SupplierReturnId },
                        principalSchema: "procurement",
                        principalTable: "SupplierReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnAudit_TenantId_ActorId_OperationId_IdempotencyKey",
                schema: "procurement",
                table: "SupplierReturnAudit",
                columns: new[] { "TenantId", "ActorId", "OperationId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnAudit_TenantId_Id",
                schema: "procurement",
                table: "SupplierReturnAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnAudit_TenantId_SupplierReturnId_OccurredAt",
                schema: "procurement",
                table: "SupplierReturnAudit",
                columns: new[] { "TenantId", "SupplierReturnId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnEvidence_TenantId_Id",
                schema: "procurement",
                table: "SupplierReturnEvidence",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnEvidence_TenantId_SupplierReturnId_RecordedAt",
                schema: "procurement",
                table: "SupplierReturnEvidence",
                columns: new[] { "TenantId", "SupplierReturnId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHistory_TenantId_Id",
                schema: "procurement",
                table: "SupplierReturnHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHistory_TenantId_SupplierReturnId_OccurredAt",
                schema: "procurement",
                table: "SupplierReturnHistory",
                columns: new[] { "TenantId", "SupplierReturnId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_TenantId_GoodsReceiptLineId",
                schema: "procurement",
                table: "SupplierReturnLines",
                columns: new[] { "TenantId", "GoodsReceiptLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_TenantId_Id",
                schema: "procurement",
                table: "SupplierReturnLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_TenantId_PurchaseOrderLineId",
                schema: "procurement",
                table: "SupplierReturnLines",
                columns: new[] { "TenantId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_TenantId_SupplierReturnId",
                schema: "procurement",
                table: "SupplierReturnLines",
                columns: new[] { "TenantId", "SupplierReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_TenantId_GoodsReceiptId_Status",
                schema: "procurement",
                table: "SupplierReturns",
                columns: new[] { "TenantId", "GoodsReceiptId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_TenantId_Id",
                schema: "procurement",
                table: "SupplierReturns",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_TenantId_PurchaseOrderId",
                schema: "procurement",
                table: "SupplierReturns",
                columns: new[] { "TenantId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_TenantId_Status_UpdatedAt",
                schema: "procurement",
                table: "SupplierReturns",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_TenantId_SupplierId_CreatedAt",
                schema: "procurement",
                table: "SupplierReturns",
                columns: new[] { "TenantId", "SupplierId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierReturnAudit",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierReturnEvidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierReturnHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierReturnLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierReturns",
                schema: "procurement");
        }
    }
}
