using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP130InventoryCountLedgerFence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SnapshotWarehouseMovementCount",
                schema: "inventory",
                table: "Counts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SnapshotIdentityMovementCount",
                schema: "inventory",
                table: "CountLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "CountSnapshots",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundGeneration = table.Column<int>(type: "int", nullable: false),
                    SnapshotCutoff = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SnapshotWarehouseMovementCount = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountSnapshots_Counts_TenantId_CountId",
                        columns: x => new { x.TenantId, x.CountId },
                        principalSchema: "inventory",
                        principalTable: "Counts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CountSnapshots_TenantId_CountId_RoundGeneration",
                schema: "inventory",
                table: "CountSnapshots",
                columns: new[] { "TenantId", "CountId", "RoundGeneration" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CountSnapshots_TenantId_Id",
                schema: "inventory",
                table: "CountSnapshots",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CountSnapshots",
                schema: "inventory");

            migrationBuilder.DropColumn(
                name: "SnapshotWarehouseMovementCount",
                schema: "inventory",
                table: "Counts");

            migrationBuilder.DropColumn(
                name: "SnapshotIdentityMovementCount",
                schema: "inventory",
                table: "CountLines");
        }
    }
}
