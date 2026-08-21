using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP128StockIntegrityRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "inventory",
                table: "OpeningBalanceRows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SourceIdentityConsumed",
                schema: "inventory",
                table: "OpeningBalanceRows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceRows_TenantId_SourceFingerprint",
                schema: "inventory",
                table: "OpeningBalanceRows",
                columns: new[] { "TenantId", "SourceFingerprint" },
                unique: true,
                filter: "[SourceIdentityConsumed] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpeningBalanceRows_TenantId_SourceFingerprint",
                schema: "inventory",
                table: "OpeningBalanceRows");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "inventory",
                table: "OpeningBalanceRows");

            migrationBuilder.DropColumn(
                name: "SourceIdentityConsumed",
                schema: "inventory",
                table: "OpeningBalanceRows");
        }
    }
}
