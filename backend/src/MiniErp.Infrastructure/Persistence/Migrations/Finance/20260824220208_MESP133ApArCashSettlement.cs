using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP133ApArCashSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashAccounts",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LinkedAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAccountCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Lifecycle = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashAccounts", x => x.Id);
                    table.UniqueConstraint("AK_CashAccounts_TenantId_CompanyId_Id", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_CashAccounts_Accounts_TenantId_CompanyId_LinkedAccountId",
                        columns: x => new { x.TenantId, x.CompanyId, x.LinkedAccountId },
                        principalSchema: "finance",
                        principalTable: "Accounts",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenItems",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceContract = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentVersion = table.Column<int>(type: "int", nullable: false),
                    SourceEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEvidenceVersion = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OriginalFunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    PaymentTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentTermCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PaymentTermEnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PaymentTermArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PaymentTermVersionNumber = table.Column<int>(type: "int", nullable: true),
                    PaymentTermVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentTermEffectiveOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MatchEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchEvidenceVersion = table.Column<int>(type: "int", nullable: true),
                    RecognitionState = table.Column<int>(type: "int", nullable: false),
                    RecognitionJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSnapshot = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenItems", x => x.Id);
                    table.UniqueConstraint("AK_OpenItems_TenantId_CompanyId_Id", x => new { x.TenantId, x.CompanyId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Lifecycle = table.Column<int>(type: "int", nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    RequiresReference = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.UniqueConstraint("AK_PaymentMethods_TenantId_CompanyId_Id", x => new { x.TenantId, x.CompanyId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "SettlementDocuments",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementDocuments", x => x.Id);
                    table.UniqueConstraint("AK_SettlementDocuments_TenantId_CompanyId_Id", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_SettlementDocuments_CashAccounts_TenantId_CompanyId_CashAccountId",
                        columns: x => new { x.TenantId, x.CompanyId, x.CashAccountId },
                        principalSchema: "finance",
                        principalTable: "CashAccounts",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementDocuments_PaymentMethods_TenantId_CompanyId_PaymentMethodId",
                        columns: x => new { x.TenantId, x.CompanyId, x.PaymentMethodId },
                        principalSchema: "finance",
                        principalTable: "PaymentMethods",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Allocations",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    AllocationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReversalOfAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allocations_OpenItems_TenantId_CompanyId_OpenItemId",
                        columns: x => new { x.TenantId, x.CompanyId, x.OpenItemId },
                        principalSchema: "finance",
                        principalTable: "OpenItems",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Allocations_SettlementDocuments_TenantId_CompanyId_SettlementDocumentId",
                        columns: x => new { x.TenantId, x.CompanyId, x.SettlementDocumentId },
                        principalSchema: "finance",
                        principalTable: "SettlementDocuments",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_TenantId_CompanyId_OpenItemId",
                schema: "finance",
                table: "Allocations",
                columns: new[] { "TenantId", "CompanyId", "OpenItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_TenantId_CompanyId_ReversalOfAllocationId",
                schema: "finance",
                table: "Allocations",
                columns: new[] { "TenantId", "CompanyId", "ReversalOfAllocationId" },
                unique: true,
                filter: "[ReversalOfAllocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_TenantId_CompanyId_SettlementDocumentId_Id",
                schema: "finance",
                table: "Allocations",
                columns: new[] { "TenantId", "CompanyId", "SettlementDocumentId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashAccounts_TenantId_CompanyId_Code",
                schema: "finance",
                table: "CashAccounts",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashAccounts_TenantId_CompanyId_LinkedAccountId",
                schema: "finance",
                table: "CashAccounts",
                columns: new[] { "TenantId", "CompanyId", "LinkedAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenItems_TenantId_CompanyId_Kind_DueDate",
                schema: "finance",
                table: "OpenItems",
                columns: new[] { "TenantId", "CompanyId", "Kind", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenItems_TenantId_CompanyId_SourceContract_SourceEvidenceId_SourceEvidenceVersion",
                schema: "finance",
                table: "OpenItems",
                columns: new[] { "TenantId", "CompanyId", "SourceContract", "SourceEvidenceId", "SourceEvidenceVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_TenantId_CompanyId_Code",
                schema: "finance",
                table: "PaymentMethods",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementDocuments_TenantId_CompanyId_CashAccountId",
                schema: "finance",
                table: "SettlementDocuments",
                columns: new[] { "TenantId", "CompanyId", "CashAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementDocuments_TenantId_CompanyId_Direction_DocumentDate",
                schema: "finance",
                table: "SettlementDocuments",
                columns: new[] { "TenantId", "CompanyId", "Direction", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementDocuments_TenantId_CompanyId_Id",
                schema: "finance",
                table: "SettlementDocuments",
                columns: new[] { "TenantId", "CompanyId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementDocuments_TenantId_CompanyId_PaymentMethodId",
                schema: "finance",
                table: "SettlementDocuments",
                columns: new[] { "TenantId", "CompanyId", "PaymentMethodId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "OpenItems",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "SettlementDocuments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "CashAccounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "PaymentMethods",
                schema: "finance");
        }
    }
}
