using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Tenancy
{
    /// <inheritdoc />
    public partial class InitialTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "TenantOwnedRecords",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RelationshipKind = table.Column<int>(type: "int", nullable: false),
                    RelatedTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantOwnedRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantOwnedRecords_TenantId_BusinessKey",
                schema: "tenancy",
                table: "TenantOwnedRecords",
                columns: new[] { "TenantId", "BusinessKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantOwnedRecords",
                schema: "tenancy");
        }
    }
}
