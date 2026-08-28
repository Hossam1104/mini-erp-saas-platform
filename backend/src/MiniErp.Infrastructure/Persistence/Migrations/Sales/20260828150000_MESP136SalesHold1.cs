#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales;

public partial class MESP136SalesHold1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrentApprovalsJson",
            schema: "sales",
            table: "SalesOrders",
            type: "nvarchar(max)",
            maxLength: 32768,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<int>(
            name: "RevisionNumber",
            schema: "sales",
            table: "SalesOrders",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "SnapshotJson",
            schema: "sales",
            table: "SalesHistory",
            type: "nvarchar(max)",
            maxLength: 131072,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CurrentApprovalsJson", schema: "sales", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "RevisionNumber", schema: "sales", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "SnapshotJson", schema: "sales", table: "SalesHistory");
    }
}

#pragma warning restore CS1591
