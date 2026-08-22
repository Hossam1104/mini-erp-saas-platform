using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP129PhysicalStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<Guid>(
                name: "GoodsReceiptId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GoodsReceiptLineId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderLineId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierReturnId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierReturnLineId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferLineId",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationStatus",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "int",
                nullable: false,
                // Existing opening/correction movements already carry cost and currency.
                // New physical movements explicitly use Pending and never persist fake zero cost.
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Transfers",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceWarehouseCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceWarehouseName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationWarehouseCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DestinationWarehouseName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.UniqueConstraint("AK_Transfers_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "TransferLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferLines", x => x.Id);
                    table.UniqueConstraint("AK_TransferLines_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_TransferLines_Transfers_TenantId_TransferId",
                        columns: x => new { x.TenantId, x.TransferId },
                        principalSchema: "inventory",
                        principalTable: "Transfers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferEvents",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferEvents_TransferLines_TenantId_TransferLineId",
                        columns: x => new { x.TenantId, x.TransferLineId },
                        principalSchema: "inventory",
                        principalTable: "TransferLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferEvents_Transfers_TenantId_TransferId",
                        columns: x => new { x.TenantId, x.TransferId },
                        principalSchema: "inventory",
                        principalTable: "Transfers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferEvents_TenantId_Id",
                schema: "inventory",
                table: "TransferEvents",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferEvents_TenantId_TransferId_EventType_Reference",
                schema: "inventory",
                table: "TransferEvents",
                columns: new[] { "TenantId", "TransferId", "EventType", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferEvents_TenantId_TransferId_OccurredAt",
                schema: "inventory",
                table: "TransferEvents",
                columns: new[] { "TenantId", "TransferId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferEvents_TenantId_TransferLineId",
                schema: "inventory",
                table: "TransferEvents",
                columns: new[] { "TenantId", "TransferLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferLines_TenantId_Id",
                schema: "inventory",
                table: "TransferLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferLines_TenantId_TransferId",
                schema: "inventory",
                table: "TransferLines",
                columns: new[] { "TenantId", "TransferId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TenantId_CompanyId_BranchId_Status_CreatedAt",
                schema: "inventory",
                table: "Transfers",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TenantId_Id",
                schema: "inventory",
                table: "Transfers",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferEvents",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "TransferLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Transfers",
                schema: "inventory");

            migrationBuilder.DropColumn(
                name: "GoodsReceiptId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "GoodsReceiptLineId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderLineId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "SupplierReturnId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "SupplierReturnLineId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "TransferId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "TransferLineId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "ValuationStatus",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}
