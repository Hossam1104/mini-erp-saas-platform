using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP128OpusStockIntegrityRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConcurrencyAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingKey",
                schema: "inventory",
                table: "ConcurrencyAnchors");

            migrationBuilder.AddColumn<long>(
                name: "TouchSequence",
                schema: "inventory",
                table: "ConcurrencyAnchors",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_ConcurrencyAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingKey",
                schema: "inventory",
                table: "ConcurrencyAnchors",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConcurrencyAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingKey",
                schema: "inventory",
                table: "ConcurrencyAnchors");

            migrationBuilder.DropColumn(
                name: "TouchSequence",
                schema: "inventory",
                table: "ConcurrencyAnchors");

            migrationBuilder.CreateIndex(
                name: "IX_ConcurrencyAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingKey",
                schema: "inventory",
                table: "ConcurrencyAnchors",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingKey" },
                unique: true,
                filter: "[BranchId] IS NOT NULL");
        }
    }
}
