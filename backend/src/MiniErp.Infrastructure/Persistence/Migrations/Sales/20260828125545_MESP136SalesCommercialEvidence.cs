using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP136SalesCommercialEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExchangeRateJson",
                schema: "sales",
                table: "SalesQuotations",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeRateJson",
                schema: "sales",
                table: "SalesOrders",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRateJson",
                schema: "sales",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "ExchangeRateJson",
                schema: "sales",
                table: "SalesOrders");
        }
    }
}
