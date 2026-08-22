using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP130SolAcceptanceRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentStageApproverIdsJson",
                schema: "inventory",
                table: "StockIssues",
                type: "nvarchar(max)",
                maxLength: 32768,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStageApproverIdsJson",
                schema: "inventory",
                table: "Adjustments",
                type: "nvarchar(max)",
                maxLength: 32768,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgerMovements_TenantId_CorrectionOfMovementId",
                schema: "inventory",
                table: "StockLedgerMovements",
                columns: new[] { "TenantId", "CorrectionOfMovementId" },
                unique: true,
                filter: "[CorrectionOfMovementId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockLedgerMovements_TenantId_CorrectionOfMovementId",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "CurrentStageApproverIdsJson",
                schema: "inventory",
                table: "StockIssues");

            migrationBuilder.DropColumn(
                name: "CurrentStageApproverIdsJson",
                schema: "inventory",
                table: "Adjustments");
        }
    }
}
