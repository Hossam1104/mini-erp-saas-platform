using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP137Hold1Remediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentTermJson",
                schema: "sales",
                table: "SalesQuotations",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTermJson",
                schema: "sales",
                table: "SalesOrders",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoffJson",
                schema: "sales",
                table: "SalesInvoiceRequests",
                type: "nvarchar(max)",
                maxLength: 16384,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentTermJson",
                schema: "sales",
                table: "SalesInvoiceRequests",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoffJson",
                schema: "sales",
                table: "SalesDeliveries",
                type: "nvarchar(max)",
                maxLength: 16384,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentTermJson",
                schema: "sales",
                table: "SalesQuotations");

            migrationBuilder.DropColumn(
                name: "PaymentTermJson",
                schema: "sales",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "HandoffJson",
                schema: "sales",
                table: "SalesInvoiceRequests");

            migrationBuilder.DropColumn(
                name: "PaymentTermJson",
                schema: "sales",
                table: "SalesInvoiceRequests");

            migrationBuilder.DropColumn(
                name: "HandoffJson",
                schema: "sales",
                table: "SalesDeliveries");
        }
    }
}
