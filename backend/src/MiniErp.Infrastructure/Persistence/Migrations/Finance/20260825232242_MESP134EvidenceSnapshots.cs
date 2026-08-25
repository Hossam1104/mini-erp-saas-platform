using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP134EvidenceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MonetaryEvidenceJson",
                schema: "finance",
                table: "TaxAccountingEffects",
                type: "nvarchar(max)",
                maxLength: 16384,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExchangeEffectiveFrom",
                schema: "finance",
                table: "RevaluationLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExchangeEffectiveTo",
                schema: "finance",
                table: "RevaluationLines",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonetaryEvidenceJson",
                schema: "finance",
                table: "TaxAccountingEffects");

            migrationBuilder.DropColumn(
                name: "ExchangeEffectiveFrom",
                schema: "finance",
                table: "RevaluationLines");

            migrationBuilder.DropColumn(
                name: "ExchangeEffectiveTo",
                schema: "finance",
                table: "RevaluationLines");
        }
    }
}
