using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.BusinessParties
{
    /// <inheritdoc />
    public partial class InitialBusinessParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "businessparties");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "businessparties",
                columns: table => new
                {
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationPath = table.Column<int>(type: "int", nullable: false),
                    ResourceKind = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    PolicyOutcome = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    BeforeSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopePolicyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScopePolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ScopeAnchorKind = table.Column<int>(type: "int", nullable: true),
                    ScopeAnchorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.EvidenceId);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "businessparties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CodeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishLegalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicLegalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EnglishLegalNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicLegalNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EnglishTradingName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicTradingName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EnglishTradingNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicTradingNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.UniqueConstraint("AK_Customers_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "businessparties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CodeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishLegalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicLegalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EnglishLegalNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicLegalNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EnglishTradingName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicTradingName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EnglishTradingNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicTradingNameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RegistrationReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RegistrationKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.UniqueConstraint("AK_Suppliers_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                schema: "businessparties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalSchema: "businessparties",
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContacts",
                schema: "businessparties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Suppliers_TenantId_SupplierId",
                        columns: x => new { x.TenantId, x.SupplierId },
                        principalSchema: "businessparties",
                        principalTable: "Suppliers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_OccurredAt",
                schema: "businessparties",
                table: "AuditEvents",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_ResourceKind_ResourceId",
                schema: "businessparties",
                table: "AuditEvents",
                columns: new[] { "TenantId", "ResourceKind", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_TenantId_CustomerId",
                schema: "businessparties",
                table: "CustomerContacts",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_ArabicLegalNameKey",
                schema: "businessparties",
                table: "Customers",
                columns: new[] { "TenantId", "ArabicLegalNameKey" },
                unique: true,
                filter: "[ArabicLegalNameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_ArabicTradingNameKey",
                schema: "businessparties",
                table: "Customers",
                columns: new[] { "TenantId", "ArabicTradingNameKey" },
                unique: true,
                filter: "[ArabicTradingNameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_CodeKey",
                schema: "businessparties",
                table: "Customers",
                columns: new[] { "TenantId", "CodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_EnglishLegalNameKey",
                schema: "businessparties",
                table: "Customers",
                columns: new[] { "TenantId", "EnglishLegalNameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_EnglishTradingNameKey",
                schema: "businessparties",
                table: "Customers",
                columns: new[] { "TenantId", "EnglishTradingNameKey" },
                unique: true,
                filter: "[EnglishTradingNameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Id",
                schema: "businessparties",
                table: "Customers",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_TenantId_SupplierId",
                schema: "businessparties",
                table: "SupplierContacts",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_ArabicLegalNameKey",
                schema: "businessparties",
                table: "Suppliers",
                columns: new[] { "TenantId", "ArabicLegalNameKey" },
                unique: true,
                filter: "[ArabicLegalNameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_ArabicTradingNameKey",
                schema: "businessparties",
                table: "Suppliers",
                columns: new[] { "TenantId", "ArabicTradingNameKey" },
                unique: true,
                filter: "[ArabicTradingNameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_CodeKey",
                schema: "businessparties",
                table: "Suppliers",
                columns: new[] { "TenantId", "CodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_EnglishLegalNameKey",
                schema: "businessparties",
                table: "Suppliers",
                columns: new[] { "TenantId", "EnglishLegalNameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_EnglishTradingNameKey",
                schema: "businessparties",
                table: "Suppliers",
                columns: new[] { "TenantId", "EnglishTradingNameKey" },
                unique: true,
                filter: "[EnglishTradingNameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_Id",
                schema: "businessparties",
                table: "Suppliers",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_RegistrationKey",
                schema: "businessparties",
                table: "Suppliers",
                columns: new[] { "TenantId", "RegistrationKey" },
                unique: true,
                filter: "[RegistrationKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "businessparties");

            migrationBuilder.DropTable(
                name: "CustomerContacts",
                schema: "businessparties");

            migrationBuilder.DropTable(
                name: "SupplierContacts",
                schema: "businessparties");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "businessparties");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "businessparties");
        }
    }
}
