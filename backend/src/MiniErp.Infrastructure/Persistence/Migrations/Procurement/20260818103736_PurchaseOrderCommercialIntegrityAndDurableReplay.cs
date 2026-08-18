using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Procurement
{
    /// <inheritdoc />
    public partial class PurchaseOrderCommercialIntegrityAndDurableReplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenantId_SourceDecisionId",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.AddColumn<int>(
                name: "ReplayResponseSchemaVersion",
                schema: "procurement",
                table: "PurchaseOrderAudit",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplayResponseSnapshotJson",
                schema: "procurement",
                table: "PurchaseOrderAudit",
                type: "nvarchar(max)",
                maxLength: 1048576,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_SourceDecisionId",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "SourceDecisionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenantId_SourceDecisionId",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReplayResponseSchemaVersion",
                schema: "procurement",
                table: "PurchaseOrderAudit");

            migrationBuilder.DropColumn(
                name: "ReplayResponseSnapshotJson",
                schema: "procurement",
                table: "PurchaseOrderAudit");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_SourceDecisionId",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "SourceDecisionId" });
        }
    }
}
