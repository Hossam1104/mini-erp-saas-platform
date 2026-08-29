using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP137DeliveryInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesDeliveries",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    MovementIdsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoiceRequests",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FinanceOpenItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoiceRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_TenantId_Id",
                schema: "sales",
                table: "SalesDeliveries",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_TenantId_IdempotencyKey",
                schema: "sales",
                table: "SalesDeliveries",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDeliveries_TenantId_OrderId_Status",
                schema: "sales",
                table: "SalesDeliveries",
                columns: new[] { "TenantId", "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceRequests_TenantId_DeliveryId_Status",
                schema: "sales",
                table: "SalesInvoiceRequests",
                columns: new[] { "TenantId", "DeliveryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceRequests_TenantId_Id",
                schema: "sales",
                table: "SalesInvoiceRequests",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceRequests_TenantId_IdempotencyKey",
                schema: "sales",
                table: "SalesInvoiceRequests",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceRequests_TenantId_OrderId_Status",
                schema: "sales",
                table: "SalesInvoiceRequests",
                columns: new[] { "TenantId", "OrderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesDeliveries",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesInvoiceRequests",
                schema: "sales");
        }
    }
}
