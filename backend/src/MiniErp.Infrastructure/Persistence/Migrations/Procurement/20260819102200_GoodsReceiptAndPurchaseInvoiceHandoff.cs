using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Procurement
{
    /// <inheritdoc />
    public partial class GoodsReceiptAndPurchaseInvoiceHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferenceNote = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.Id);
                    table.UniqueConstraint("AK_GoodsReceipts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceHandoffs",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SupplierInvoiceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SupplierInvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceHandoffs", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseInvoiceHandoffs_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceHandoffs_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptAudit",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_GoodsReceiptAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptAudit_GoodsReceipts_TenantId_GoodsReceiptId",
                        columns: x => new { x.TenantId, x.GoodsReceiptId },
                        principalSchema: "procurement",
                        principalTable: "GoodsReceipts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_GoodsReceiptHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptHistory_GoodsReceipts_TenantId_GoodsReceiptId",
                        columns: x => new { x.TenantId, x.GoodsReceiptId },
                        principalSchema: "procurement",
                        principalTable: "GoodsReceipts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrderedQuantityAtReceipt = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    DamagedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    DamageNotes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RemainingReceivableQuantityAfter = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptLines", x => x.Id);
                    table.UniqueConstraint("AK_GoodsReceiptLines_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_GoodsReceipts_TenantId_GoodsReceiptId",
                        columns: x => new { x.TenantId, x.GoodsReceiptId },
                        principalSchema: "procurement",
                        principalTable: "GoodsReceipts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_PurchaseOrderLines_TenantId_PurchaseOrderLineId",
                        columns: x => new { x.TenantId, x.PurchaseOrderLineId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceHandoffAudit",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PurchaseInvoiceHandoffAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceHandoffAudit_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceHandoffHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PurchaseInvoiceHandoffHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceHandoffHistory_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceHandoffLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HandoffQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxRatePercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    LineAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceHandoffLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceHandoffLines_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceHandoffLines_PurchaseOrderLines_TenantId_PurchaseOrderLineId",
                        columns: x => new { x.TenantId, x.PurchaseOrderLineId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceHandoffSources",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceHandoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceHandoffSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceHandoffSources_GoodsReceiptLines_TenantId_GoodsReceiptLineId",
                        columns: x => new { x.TenantId, x.GoodsReceiptLineId },
                        principalSchema: "procurement",
                        principalTable: "GoodsReceiptLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceHandoffSources_PurchaseInvoiceHandoffs_TenantId_PurchaseInvoiceHandoffId",
                        columns: x => new { x.TenantId, x.PurchaseInvoiceHandoffId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseInvoiceHandoffs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptAudit_TenantId_ActorId_OperationId_IdempotencyKey",
                schema: "procurement",
                table: "GoodsReceiptAudit",
                columns: new[] { "TenantId", "ActorId", "OperationId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptAudit_TenantId_GoodsReceiptId_OccurredAt",
                schema: "procurement",
                table: "GoodsReceiptAudit",
                columns: new[] { "TenantId", "GoodsReceiptId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptAudit_TenantId_Id",
                schema: "procurement",
                table: "GoodsReceiptAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptHistory_TenantId_GoodsReceiptId_OccurredAt",
                schema: "procurement",
                table: "GoodsReceiptHistory",
                columns: new[] { "TenantId", "GoodsReceiptId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptHistory_TenantId_Id",
                schema: "procurement",
                table: "GoodsReceiptHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_TenantId_GoodsReceiptId",
                schema: "procurement",
                table: "GoodsReceiptLines",
                columns: new[] { "TenantId", "GoodsReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_TenantId_Id",
                schema: "procurement",
                table: "GoodsReceiptLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_TenantId_PurchaseOrderLineId",
                schema: "procurement",
                table: "GoodsReceiptLines",
                columns: new[] { "TenantId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_TenantId_Id",
                schema: "procurement",
                table: "GoodsReceipts",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_TenantId_PurchaseOrderId_CreatedAt",
                schema: "procurement",
                table: "GoodsReceipts",
                columns: new[] { "TenantId", "PurchaseOrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_TenantId_Status_UpdatedAt",
                schema: "procurement",
                table: "GoodsReceipts",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffAudit_TenantId_ActorId_OperationId_IdempotencyKey",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffAudit",
                columns: new[] { "TenantId", "ActorId", "OperationId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffAudit_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffAudit_TenantId_PurchaseInvoiceHandoffId_OccurredAt",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffAudit",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffHistory_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffHistory_TenantId_PurchaseInvoiceHandoffId_OccurredAt",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffHistory",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffLines_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffLines_TenantId_PurchaseInvoiceHandoffId",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffLines",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffLines_TenantId_PurchaseOrderLineId",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffLines",
                columns: new[] { "TenantId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffs_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffs",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffs_TenantId_PurchaseOrderId_CreatedAt",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffs",
                columns: new[] { "TenantId", "PurchaseOrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffs_TenantId_Status_UpdatedAt",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffs",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffSources_TenantId_GoodsReceiptLineId",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffSources",
                columns: new[] { "TenantId", "GoodsReceiptLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffSources_TenantId_Id",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffSources",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceHandoffSources_TenantId_PurchaseInvoiceHandoffId",
                schema: "procurement",
                table: "PurchaseInvoiceHandoffSources",
                columns: new[] { "TenantId", "PurchaseInvoiceHandoffId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceiptAudit",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "GoodsReceiptHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceHandoffAudit",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceHandoffHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceHandoffLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceHandoffSources",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "GoodsReceiptLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceHandoffs",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "GoodsReceipts",
                schema: "procurement");
        }
    }
}
