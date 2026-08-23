using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP131MovingWeightedAverageValuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LedgerSequence",
                schema: "inventory",
                table: "StockLedgerMovements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // One-time deterministic legacy bootstrap. PostedAt is used only to
            // establish the documented migration baseline; all later posting
            // order is allocated from the durable company anchor.
            migrationBuilder.Sql("""
                ;WITH OrderedMovements AS
                (
                    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [TenantId], [CompanyId] ORDER BY [PostedAt], [Id]) AS [SequenceValue]
                    FROM [inventory].[StockLedgerMovements]
                )
                UPDATE movement
                SET [LedgerSequence] = ordered.[SequenceValue]
                FROM [inventory].[StockLedgerMovements] AS movement
                INNER JOIN OrderedMovements AS ordered ON ordered.[Id] = movement.[Id];
                """);

            migrationBuilder.CreateTable(
                name: "CompanyLedgerSequenceAnchors",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextSequence = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLedgerSequenceAnchors", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO [inventory].[CompanyLedgerSequenceAnchors] ([Id], [TenantId], [CompanyId], [NextSequence])
                SELECT NEWID(), [TenantId], [CompanyId], MAX([LedgerSequence]) + 1
                FROM [inventory].[StockLedgerMovements]
                GROUP BY [TenantId], [CompanyId];
                """);

            migrationBuilder.CreateTable(
                name: "FinanceValuationHandoffs",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LedgerSequence = table.Column<long>(type: "bigint", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationEvidenceVersion = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    BaseUnitCost = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersionNumber = table.Column<int>(type: "int", nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TransactionUnitCost = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    ExchangeRateScale = table.Column<int>(type: "int", nullable: true),
                    ExchangeRateProvenance = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrectionOfMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ContractVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceValuationHandoffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovementValuationEvents",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectionOfMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GoodsReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierReturnLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LedgerSequence = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersionNumber = table.Column<int>(type: "int", nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    TransactionUnitCost = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    ExchangeRateScale = table.Column<int>(type: "int", nullable: true),
                    ExchangeRateProvenance = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseUnitCost = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    PriorQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    PriorValue = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    NewQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    NewValue = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    MovementValue = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    UnitCostScale = table.Column<int>(type: "int", nullable: false),
                    AmountScale = table.Column<int>(type: "int", nullable: false),
                    RoundingMode = table.Column<int>(type: "int", nullable: false),
                    CorrectionOfValuationEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsBackdated = table.Column<bool>(type: "bit", nullable: false),
                    PendingReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementValuationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValuationPolicies",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScopeMode = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    UnitCostScale = table.Column<int>(type: "int", nullable: false),
                    AmountScale = table.Column<int>(type: "int", nullable: false),
                    RoundingMode = table.Column<int>(type: "int", nullable: false),
                    GoodsReceiptCostBasis = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PositiveAdjustmentCostBasis = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SupplierReturnCostBasis = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValuationRuns",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValuationScopeAnchors",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationScopeAnchors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValuationStates",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersionNumber = table.Column<int>(type: "int", nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    AverageUnitCost = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    LastAppliedLedgerSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgerMovements_TenantId_CompanyId_LedgerSequence",
                schema: "inventory",
                table: "StockLedgerMovements",
                columns: new[] { "TenantId", "CompanyId", "LedgerSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedgerSequenceAnchors_TenantId_CompanyId",
                schema: "inventory",
                table: "CompanyLedgerSequenceAnchors",
                columns: new[] { "TenantId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedgerSequenceAnchors_TenantId_Id",
                schema: "inventory",
                table: "CompanyLedgerSequenceAnchors",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceValuationHandoffs_TenantId_CompanyId_BranchId_WarehouseId_LedgerSequence",
                schema: "inventory",
                table: "FinanceValuationHandoffs",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "LedgerSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceValuationHandoffs_TenantId_Id",
                schema: "inventory",
                table: "FinanceValuationHandoffs",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceValuationHandoffs_TenantId_MovementId",
                schema: "inventory",
                table: "FinanceValuationHandoffs",
                columns: new[] { "TenantId", "MovementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovementValuationEvents_TenantId_CompanyId_LedgerSequence",
                schema: "inventory",
                table: "MovementValuationEvents",
                columns: new[] { "TenantId", "CompanyId", "LedgerSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_MovementValuationEvents_TenantId_Id",
                schema: "inventory",
                table: "MovementValuationEvents",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovementValuationEvents_TenantId_MovementId_Status",
                schema: "inventory",
                table: "MovementValuationEvents",
                columns: new[] { "TenantId", "MovementId", "Status" },
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_MovementValuationEvents_TenantId_SourceRevisionId",
                schema: "inventory",
                table: "MovementValuationEvents",
                columns: new[] { "TenantId", "SourceRevisionId" },
                unique: true,
                filter: "[SourceRevisionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationPolicies_TenantId_CompanyId_EffectiveFrom_EffectiveTo",
                schema: "inventory",
                table: "ValuationPolicies",
                columns: new[] { "TenantId", "CompanyId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_ValuationPolicies_TenantId_Id",
                schema: "inventory",
                table: "ValuationPolicies",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationRuns_TenantId_ActorId_IdempotencyKey",
                schema: "inventory",
                table: "ValuationRuns",
                columns: new[] { "TenantId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationRuns_TenantId_Id",
                schema: "inventory",
                table: "ValuationRuns",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationScopeAnchors_TenantId_Id",
                schema: "inventory",
                table: "ValuationScopeAnchors",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationScopeAnchors_TenantId_PolicyId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationScopeAnchors",
                columns: new[] { "TenantId", "PolicyId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationStates_TenantId_Id",
                schema: "inventory",
                table: "ValuationStates",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationStates_TenantId_PolicyId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationStates",
                columns: new[] { "TenantId", "PolicyId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingIdentity" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyLedgerSequenceAnchors",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "FinanceValuationHandoffs",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "MovementValuationEvents",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ValuationPolicies",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ValuationRuns",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ValuationScopeAnchors",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ValuationStates",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "IX_StockLedgerMovements_TenantId_CompanyId_LedgerSequence",
                schema: "inventory",
                table: "StockLedgerMovements");

            migrationBuilder.DropColumn(
                name: "LedgerSequence",
                schema: "inventory",
                table: "StockLedgerMovements");
        }
    }
}
