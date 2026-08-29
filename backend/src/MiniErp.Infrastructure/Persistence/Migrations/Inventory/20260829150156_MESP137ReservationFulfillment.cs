using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP137ReservationFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FulfilledQuantity",
                schema: "inventory",
                table: "Reservations",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                schema: "inventory",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceLineId",
                schema: "inventory",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRevision",
                schema: "inventory",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FulfilledQuantityAfter",
                schema: "inventory",
                table: "ReservationHistory",
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
                name: "FulfilledQuantity",
                schema: "inventory",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                schema: "inventory",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SourceLineId",
                schema: "inventory",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SourceRevision",
                schema: "inventory",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "FulfilledQuantityAfter",
                schema: "inventory",
                table: "ReservationHistory");
        }
    }
}
