using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Procurement
{
    /// <inheritdoc />
    public partial class MESP126ResolutionPolicyEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionPolicySnapshotJson",
                schema: "procurement",
                table: "PurchaseInvoiceMatchEvaluations",
                type: "nvarchar(max)",
                maxLength: 32768,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionPolicySnapshotJson",
                schema: "procurement",
                table: "PurchaseInvoiceMatchEvaluations");
        }
    }
}
