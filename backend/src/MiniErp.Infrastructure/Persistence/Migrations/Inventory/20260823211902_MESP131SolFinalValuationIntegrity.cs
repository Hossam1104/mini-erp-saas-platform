using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory;

/// <inheritdoc />
public partial class MESP131SolFinalValuationIntegrity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "FormulaMovementValue",
            schema: "inventory",
            table: "MovementValuationEvents",
            type: "decimal(28,8)",
            precision: 28,
            scale: 8,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RoundingAdjustmentAmount",
            schema: "inventory",
            table: "MovementValuationEvents",
            type: "decimal(28,8)",
            precision: 28,
            scale: 8,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RoundingAdjustmentAmount",
            schema: "inventory",
            table: "FinanceValuationHandoffs",
            type: "decimal(28,8)",
            precision: 28,
            scale: 8,
            nullable: false,
            defaultValue: 0m);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FormulaMovementValue",
            schema: "inventory",
            table: "MovementValuationEvents");

        migrationBuilder.DropColumn(
            name: "RoundingAdjustmentAmount",
            schema: "inventory",
            table: "MovementValuationEvents");

        migrationBuilder.DropColumn(
            name: "RoundingAdjustmentAmount",
            schema: "inventory",
            table: "FinanceValuationHandoffs");
    }
}
