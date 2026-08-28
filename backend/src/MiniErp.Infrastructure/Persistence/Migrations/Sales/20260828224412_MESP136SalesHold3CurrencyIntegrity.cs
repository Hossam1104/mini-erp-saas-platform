using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP136SalesHold3CurrencyIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "sales",
                table: "SalesCreditEvaluations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<decimal>(
                name: "ConvertedOrderCommitment",
                schema: "sales",
                table: "SalesCreditEvaluations",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeRateJson",
                schema: "sales",
                table: "SalesCreditEvaluations",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderRevisionNumber",
                schema: "sales",
                table: "SalesCreditEvaluations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionAmount",
                schema: "sales",
                table: "SalesCreditEvaluations",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionCurrencyCode",
                schema: "sales",
                table: "SalesCreditEvaluations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConvertedOrderCommitment",
                schema: "sales",
                table: "SalesCreditEvaluations");

            migrationBuilder.DropColumn(
                name: "ExchangeRateJson",
                schema: "sales",
                table: "SalesCreditEvaluations");

            migrationBuilder.DropColumn(
                name: "OrderRevisionNumber",
                schema: "sales",
                table: "SalesCreditEvaluations");

            migrationBuilder.DropColumn(
                name: "TransactionAmount",
                schema: "sales",
                table: "SalesCreditEvaluations");

            migrationBuilder.DropColumn(
                name: "TransactionCurrencyCode",
                schema: "sales",
                table: "SalesCreditEvaluations");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "sales",
                table: "SalesCreditEvaluations",
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
